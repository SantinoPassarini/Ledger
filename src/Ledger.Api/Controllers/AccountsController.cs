using Ledger.Api.Contracts.Accounts;
using Ledger.Api.Data;
using Ledger.Api.Domain;
using Ledger.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ledger.Api.Controllers;

[ApiController]
[Route("api/v1/accounts")]
public sealed class AccountsController : ControllerBase
{
    private readonly LedgerDbContext _db;
    private readonly LedgerOptions _options;

    public AccountsController(LedgerDbContext db, IOptions<LedgerOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> GetAll(
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Accounts.AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(account => !account.IsArchived);
        }

        var accounts = await query
            .OrderBy(account => account.Name)
            .ToListAsync(cancellationToken);

        var balances = await GetBalancesAsync(cancellationToken);

        var response = accounts
            .Select(account => ToResponse(account, balances.GetValueOrDefault(account.Id)))
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (account is null)
        {
            return NotFound();
        }

        var balance = await GetBalanceAsync(id, cancellationToken);

        return Ok(ToResponse(account, balance));
    }

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
            return ValidationProblem(ModelState);
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = request.Type!.Value,
            Currency = _options.Currency,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = account.Id },
            ToResponse(account, 0m));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);

        if (account is null)
        {
            return NotFound();
        }

        if (account.IsArchived)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Account is archived",
                detail: "Archived accounts cannot be modified.");
        }

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
            return ValidationProblem(ModelState);
        }

        account.Name = name;
        account.Type = request.Type!.Value;

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);

        if (account is null)
        {
            return NotFound();
        }

        if (!account.IsArchived)
        {
            account.IsArchived = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private async Task<Dictionary<Guid, decimal>> GetBalancesAsync(CancellationToken cancellationToken)
    {
        return await _db.Transactions
            .AsNoTracking()
            .Where(transaction => !transaction.IsArchived)
            .GroupBy(transaction => transaction.AccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                Balance = group.Sum(transaction =>
                    transaction.Type == TransactionType.Income ||
                    transaction.Type == TransactionType.TransferIn
                        ? transaction.Amount
                        : -transaction.Amount)
            })
            .ToDictionaryAsync(item => item.AccountId, item => item.Balance, cancellationToken);
    }

    private Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return _db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.AccountId == accountId &&
                !transaction.IsArchived)
            .SumAsync(transaction =>
                transaction.Type == TransactionType.Income ||
                transaction.Type == TransactionType.TransferIn
                    ? transaction.Amount
                    : -transaction.Amount, cancellationToken);
    }

    private static AccountResponse ToResponse(Account account, decimal balance)
    {
        return new AccountResponse(
            account.Id,
            account.Name,
            account.Type,
            account.Currency,
            account.IsArchived,
            balance,
            account.CreatedAtUtc);
    }
}
