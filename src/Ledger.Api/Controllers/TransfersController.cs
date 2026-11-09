using System.Data;
using Ledger.Api.Auth;
using Ledger.Api.Contracts.Transactions;
using Ledger.Api.Contracts.Transfers;
using Ledger.Api.Data;
using Ledger.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Api.Controllers;

[ApiController]
[Route("api/v1/transfers")]
public sealed class TransfersController : ControllerBase
{
    private const decimal MaxAmount = 9_999_999_999_999_999.99m;

    private readonly LedgerDbContext _db;
    private readonly ICurrentUser _currentUser;

    public TransfersController(LedgerDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<TransferResponse>> Create(
        CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var fromAccount = await _db.Accounts.FirstOrDefaultAsync(
            candidate => candidate.Id == request.FromAccountId,
            cancellationToken);
        var toAccount = await _db.Accounts.FirstOrDefaultAsync(
            candidate => candidate.Id == request.ToAccountId,
            cancellationToken);

        if (fromAccount is null)
        {
            ModelState.AddModelError(
                nameof(CreateTransferRequest.FromAccountId),
                "Source account not found.");
        }

        if (toAccount is null)
        {
            ModelState.AddModelError(
                nameof(CreateTransferRequest.ToAccountId),
                "Destination account not found.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (fromAccount!.IsArchived)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Source account is archived",
                detail: "Transfers cannot use archived accounts.");
        }

        if (toAccount!.IsArchived)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Destination account is archived",
                detail: "Transfers cannot use archived accounts.");
        }

        var amount = request.Amount!.Value;
        var transferDate = request.Date!.Value;

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var sourceBalance = await _db.Transactions
            .AsNoTracking()
            .Where(candidate =>
                candidate.AccountId == fromAccount.Id &&
                !candidate.IsArchived)
            .SumAsync(candidate =>
                candidate.Type == TransactionType.Income ||
                candidate.Type == TransactionType.TransferIn
                    ? candidate.Amount
                    : -candidate.Amount,
                cancellationToken);

        if (sourceBalance < amount)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Insufficient funds",
                detail: $"The source account balance is {sourceBalance:N2}, which is less than the transfer amount {amount:N2}.");
        }

        var now = DateTime.UtcNow;
        var transferGroupId = Guid.NewGuid();
        var description = NormalizeDescription(request.Description);
        var userId = _currentUser.RequireUserId();

        var outTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = fromAccount.Id,
            CategoryId = null,
            Type = TransactionType.TransferOut,
            Amount = amount,
            Date = transferDate,
            Description = description,
            TransferGroupId = transferGroupId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var inTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = toAccount.Id,
            CategoryId = null,
            Type = TransactionType.TransferIn,
            Amount = amount,
            Date = transferDate,
            Description = description,
            TransferGroupId = transferGroupId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Transactions.AddRange(outTransaction, inTransaction);
        await _db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetByGroupId),
            new { groupId = transferGroupId },
            ToResponse(transferGroupId, outTransaction, fromAccount.Name, inTransaction, toAccount.Name));
    }

    [HttpGet("{groupId:guid}")]
    public async Task<ActionResult<TransferResponse>> GetByGroupId(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var transactions = await _db.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.Account)
            .Where(transaction => transaction.TransferGroupId == groupId)
            .ToListAsync(cancellationToken);

        var outTransaction = transactions.FirstOrDefault(
            transaction => transaction.Type == TransactionType.TransferOut);
        var inTransaction = transactions.FirstOrDefault(
            transaction => transaction.Type == TransactionType.TransferIn);

        if (outTransaction is null || inTransaction is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(groupId, outTransaction, inTransaction));
    }

    [HttpDelete("{groupId:guid}")]
    public async Task<IActionResult> Archive(Guid groupId, CancellationToken cancellationToken)
    {
        var transactions = await _db.Transactions
            .Where(transaction => transaction.TransferGroupId == groupId)
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;

        foreach (var transaction in transactions.Where(transaction => !transaction.IsArchived))
        {
            transaction.IsArchived = true;
            transaction.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private void ValidateRequest(CreateTransferRequest request)
    {
        if (request.FromAccountId == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(CreateTransferRequest.FromAccountId),
                "FromAccountId is required.");
        }

        if (request.ToAccountId == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(CreateTransferRequest.ToAccountId),
                "ToAccountId is required.");
        }

        if (request.FromAccountId != Guid.Empty &&
            request.FromAccountId == request.ToAccountId)
        {
            ModelState.AddModelError(
                nameof(CreateTransferRequest.ToAccountId),
                "Source and destination accounts must be different.");
        }

        if (request.Date is null || request.Date == default)
        {
            ModelState.AddModelError(
                nameof(CreateTransferRequest.Date),
                "Date is required.");
        }

        if (request.Amount is null || request.Amount <= 0)
        {
            ModelState.AddModelError(
                nameof(CreateTransferRequest.Amount),
                "Amount must be greater than zero.");
        }
        else if (decimal.Round(request.Amount.Value, 2) != request.Amount.Value)
        {
            ModelState.AddModelError(
                nameof(CreateTransferRequest.Amount),
                "Amount must have at most 2 decimal places.");
        }
        else if (request.Amount > MaxAmount)
        {
            ModelState.AddModelError(
                nameof(CreateTransferRequest.Amount),
                "Amount is too large.");
        }
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static TransferResponse ToResponse(
        Guid transferGroupId,
        Transaction outTransaction,
        string outAccountName,
        Transaction inTransaction,
        string inAccountName)
    {
        return new TransferResponse(
            transferGroupId,
            ToTransactionResponse(outTransaction, outAccountName),
            ToTransactionResponse(inTransaction, inAccountName));
    }

    private static TransferResponse ToResponse(
        Guid transferGroupId,
        Transaction outTransaction,
        Transaction inTransaction)
    {
        return ToResponse(
            transferGroupId,
            outTransaction,
            outTransaction.Account.Name,
            inTransaction,
            inTransaction.Account.Name);
    }

    private static TransactionResponse ToTransactionResponse(
        Transaction transaction,
        string accountName)
    {
        return new TransactionResponse(
            transaction.Id,
            transaction.AccountId,
            accountName,
            transaction.CategoryId,
            transaction.Category?.Name,
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
