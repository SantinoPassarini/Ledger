using Ledger.Api.Contracts.Dashboard;
using Ledger.Api.Contracts.Transactions;
using Ledger.Api.Data;
using Ledger.Api.Domain;
using Ledger.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ledger.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private const int RecentTransactionCount = 8;
    private const int TrendMonthCount = 6;

    private readonly LedgerDbContext _db;
    private readonly LedgerOptions _options;

    public DashboardController(LedgerDbContext db, IOptions<LedgerOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(
        [FromQuery] DateOnly? month,
        CancellationToken cancellationToken)
    {
        var selectedMonth = month ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(selectedMonth.Year, selectedMonth.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var trendStart = monthStart.AddMonths(-(TrendMonthCount - 1));

        var accountBalances = await GetAccountBalancesAsync(cancellationToken);
        var totalBalance = accountBalances.Sum(account => account.Balance);

        var trendRows = await _db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                !transaction.IsArchived &&
                !transaction.Account.IsArchived &&
                transaction.Date >= trendStart &&
                transaction.Date <= monthEnd &&
                (transaction.Type == TransactionType.Income ||
                 transaction.Type == TransactionType.Expense))
            .Select(transaction => new
            {
                transaction.Date,
                transaction.Type,
                transaction.Amount
            })
            .ToListAsync(cancellationToken);

        var totalsByMonth = trendRows
            .GroupBy(transaction => new DateOnly(
                transaction.Date.Year,
                transaction.Date.Month,
                1))
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Income = group
                        .Where(transaction => transaction.Type == TransactionType.Income)
                        .Sum(transaction => transaction.Amount),
                    Expenses = group
                        .Where(transaction => transaction.Type == TransactionType.Expense)
                        .Sum(transaction => transaction.Amount)
                });

        var monthlyTrend = new List<DashboardMonthlyTotal>(TrendMonthCount);

        for (var offset = 0; offset < TrendMonthCount; offset++)
        {
            var trendMonth = trendStart.AddMonths(offset);
            totalsByMonth.TryGetValue(trendMonth, out var totals);

            monthlyTrend.Add(new DashboardMonthlyTotal(
                trendMonth,
                totals?.Income ?? 0m,
                totals?.Expenses ?? 0m));
        }

        var currentMonth = monthlyTrend[^1];

        var expenseRows = await _db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                !transaction.IsArchived &&
                !transaction.Account.IsArchived &&
                transaction.Type == TransactionType.Expense &&
                transaction.Date >= monthStart &&
                transaction.Date <= monthEnd)
            .Select(transaction => new
            {
                transaction.CategoryId,
                CategoryName = transaction.Category != null
                    ? transaction.Category.Name
                    : "Uncategorized",
                transaction.Amount
            })
            .ToListAsync(cancellationToken);

        var expensesByCategory = expenseRows
            .GroupBy(expense => new { expense.CategoryId, expense.CategoryName })
            .Select(group => new DashboardCategoryTotal(
                group.Key.CategoryId,
                group.Key.CategoryName,
                group.Sum(expense => expense.Amount)))
            .OrderByDescending(category => category.Amount)
            .ThenBy(category => category.CategoryName)
            .ToList();

        var recentTransactions = await _db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                !transaction.IsArchived &&
                !transaction.Account.IsArchived)
            .OrderByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.CreatedAtUtc)
            .ThenBy(transaction => transaction.Id)
            .Take(RecentTransactionCount)
            .Select(transaction => new TransactionResponse(
                transaction.Id,
                transaction.AccountId,
                transaction.Account.Name,
                transaction.CategoryId,
                transaction.Category != null ? transaction.Category.Name : null,
                transaction.Type,
                transaction.Amount,
                transaction.Date,
                transaction.Description,
                transaction.TransferGroupId,
                transaction.IsArchived,
                transaction.CreatedAtUtc,
                transaction.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new DashboardResponse(
            monthStart,
            _options.Currency,
            totalBalance,
            currentMonth.Income,
            currentMonth.Expenses,
            accountBalances,
            expensesByCategory,
            monthlyTrend,
            recentTransactions));
    }

    private async Task<IReadOnlyList<DashboardAccountBalance>> GetAccountBalancesAsync(
        CancellationToken cancellationToken)
    {
        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(account => !account.IsArchived)
            .OrderBy(account => account.Name)
            .Select(account => new { account.Id, account.Name })
            .ToListAsync(cancellationToken);

        var balances = await _db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                !transaction.IsArchived &&
                !transaction.Account.IsArchived)
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
            .ToDictionaryAsync(
                item => item.AccountId,
                item => item.Balance,
                cancellationToken);

        return accounts
            .Select(account => new DashboardAccountBalance(
                account.Id,
                account.Name,
                balances.GetValueOrDefault(account.Id)))
            .ToList();
    }
}
