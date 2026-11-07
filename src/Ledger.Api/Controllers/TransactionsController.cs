using Ledger.Api.Contracts.Common;
using Ledger.Api.Contracts.Transactions;
using Ledger.Api.Data;
using Ledger.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Api.Controllers;

[ApiController]
[Route("api/v1/transactions")]
public sealed class TransactionsController : ControllerBase
{
    private const decimal MaxAmount = 9_999_999_999_999_999.99m;

    private readonly LedgerDbContext _db;

    public TransactionsController(LedgerDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<TransactionResponse>>> GetAll(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? categoryId,
        [FromQuery] TransactionType? type,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "Page must be greater than or equal to 1.");
        }

        if (pageSize is < 1 or > 200)
        {
            ModelState.AddModelError(nameof(pageSize), "Page size must be between 1 and 200.");
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            ModelState.AddModelError(nameof(from), "'from' must be earlier than or equal to 'to'.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var query = _db.Transactions.AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(transaction => !transaction.IsArchived);
        }

        if (from.HasValue)
        {
            query = query.Where(transaction => transaction.Date >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(transaction => transaction.Date <= to.Value);
        }

        if (accountId.HasValue)
        {
            query = query.Where(transaction => transaction.AccountId == accountId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(transaction => transaction.CategoryId == categoryId.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(transaction => transaction.Type == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(transaction =>
                transaction.Description != null &&
                EF.Functions.ILike(transaction.Description, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.CreatedAtUtc)
            .ThenBy(transaction => transaction.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(transaction => new TransactionResponse(
                transaction.Id,
                transaction.AccountId,
                transaction.Account.Name,
                transaction.CategoryId,
                transaction.Category.Name,
                transaction.Type,
                transaction.Amount,
                transaction.Date,
                transaction.Description,
                transaction.TransferGroupId,
                transaction.IsArchived,
                transaction.CreatedAtUtc,
                transaction.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<TransactionResponse>(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _db.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.Id == id)
            .Select(transaction => new TransactionResponse(
                transaction.Id,
                transaction.AccountId,
                transaction.Account.Name,
                transaction.CategoryId,
                transaction.Category.Name,
                transaction.Type,
                transaction.Amount,
                transaction.Date,
                transaction.Description,
                transaction.TransferGroupId,
                transaction.IsArchived,
                transaction.CreatedAtUtc,
                transaction.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(
        CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var (account, category, error) = await ValidateForWriteAsync(
            request.AccountId,
            request.CategoryId,
            request.Type!.Value,
            request.Amount!.Value,
            request.Date!.Value,
            cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var now = DateTime.UtcNow;

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account!.Id,
            CategoryId = category!.Id,
            Type = request.Type.Value,
            Amount = request.Amount.Value,
            Date = request.Date.Value,
            Description = NormalizeDescription(request.Description),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = transaction.Id },
            ToResponse(transaction, account.Name, category.Name));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var transaction = await _db.Transactions.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);

        if (transaction is null)
        {
            return NotFound();
        }

        if (transaction.IsArchived)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Transaction is archived",
                detail: "Archived transactions cannot be modified.");
        }

        var (account, category, error) = await ValidateForWriteAsync(
            request.AccountId,
            request.CategoryId,
            request.Type!.Value,
            request.Amount!.Value,
            request.Date!.Value,
            cancellationToken);

        if (error is not null)
        {
            return error;
        }

        transaction.AccountId = account!.Id;
        transaction.CategoryId = category!.Id;
        transaction.Type = request.Type.Value;
        transaction.Amount = request.Amount.Value;
        transaction.Date = request.Date.Value;
        transaction.Description = NormalizeDescription(request.Description);
        transaction.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _db.Transactions.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);

        if (transaction is null)
        {
            return NotFound();
        }

        if (!transaction.IsArchived)
        {
            transaction.IsArchived = true;
            transaction.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private async Task<(Account? Account, Category? Category, ActionResult? Error)> ValidateForWriteAsync(
        Guid accountId,
        Guid categoryId,
        TransactionType type,
        decimal amount,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(CreateTransactionRequest.AccountId), "AccountId is required.");
        }

        if (categoryId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(CreateTransactionRequest.CategoryId), "CategoryId is required.");
        }

        if (date == default)
        {
            ModelState.AddModelError(nameof(CreateTransactionRequest.Date), "Date is required.");
        }

        if (amount <= 0)
        {
            ModelState.AddModelError(
                nameof(CreateTransactionRequest.Amount),
                "Amount must be greater than zero.");
        }
        else if (decimal.Round(amount, 2) != amount)
        {
            ModelState.AddModelError(
                nameof(CreateTransactionRequest.Amount),
                "Amount must have at most 2 decimal places.");
        }
        else if (amount > MaxAmount)
        {
            ModelState.AddModelError(
                nameof(CreateTransactionRequest.Amount),
                "Amount is too large.");
        }

        if (type is TransactionType.TransferOut or TransactionType.TransferIn)
        {
            ModelState.AddModelError(
                nameof(CreateTransactionRequest.Type),
                "Transfers are not supported yet.");
        }

        if (!ModelState.IsValid)
        {
            return (null, null, ValidationProblem(ModelState));
        }

        var account = await _db.Accounts.FirstOrDefaultAsync(
            candidate => candidate.Id == accountId,
            cancellationToken);

        if (account is null)
        {
            ModelState.AddModelError(nameof(CreateTransactionRequest.AccountId), "Account not found.");
        }

        var category = await _db.Categories.FirstOrDefaultAsync(
            candidate => candidate.Id == categoryId,
            cancellationToken);

        if (category is null)
        {
            ModelState.AddModelError(nameof(CreateTransactionRequest.CategoryId), "Category not found.");
        }

        if (!ModelState.IsValid)
        {
            return (null, null, ValidationProblem(ModelState));
        }

        if (account!.IsArchived)
        {
            return (null, null, Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Account is archived",
                detail: "Transactions cannot use archived accounts."));
        }

        if (category!.IsArchived)
        {
            return (null, null, Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Category is archived",
                detail: "Transactions cannot use archived categories."));
        }

        var expectedKind = type == TransactionType.Income
            ? CategoryKind.Income
            : CategoryKind.Expense;

        if (category.Kind != expectedKind)
        {
            ModelState.AddModelError(
                nameof(CreateTransactionRequest.CategoryId),
                $"Category kind must be {expectedKind} for a {type} transaction.");
            return (null, null, ValidationProblem(ModelState));
        }

        return (account, category, null);
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static TransactionResponse ToResponse(
        Transaction transaction,
        string accountName,
        string categoryName)
    {
        return new TransactionResponse(
            transaction.Id,
            transaction.AccountId,
            accountName,
            transaction.CategoryId,
            categoryName,
            transaction.Type,
            transaction.Amount,
            transaction.Date,
            transaction.Description,
            transaction.TransferGroupId,
            transaction.IsArchived,
            transaction.CreatedAtUtc,
            transaction.UpdatedAtUtc);
    }
}
