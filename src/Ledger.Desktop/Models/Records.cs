using System.Globalization;

namespace Ledger.Desktop.Models;

public sealed record AccountModel(
    Guid Id,
    string Name,
    AccountType Type,
    string Currency,
    bool IsArchived,
    decimal Balance,
    DateTime CreatedAtUtc);

public sealed record CategoryModel(
    Guid Id,
    string Name,
    CategoryKind Kind,
    bool IsArchived,
    DateTime CreatedAtUtc);

public sealed record TransactionModel(
    Guid Id,
    Guid AccountId,
    string AccountName,
    Guid? CategoryId,
    string? CategoryName,
    TransactionType Type,
    decimal Amount,
    DateOnly Date,
    string? Description,
    Guid? TransferGroupId,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record TransferModel(
    Guid TransferGroupId,
    TransactionModel OutTransaction,
    TransactionModel InTransaction);

public sealed record DashboardModel(
    DateOnly Month,
    string Currency,
    decimal TotalBalance,
    decimal MonthIncome,
    decimal MonthExpenses,
    IReadOnlyList<DashboardAccountBalanceModel> AccountBalances,
    IReadOnlyList<DashboardCategoryTotalModel> ExpensesByCategory,
    IReadOnlyList<DashboardMonthlyTotalModel> MonthlyTrend,
    IReadOnlyList<TransactionModel> RecentTransactions);

public sealed record DashboardAccountBalanceModel(
    Guid AccountId,
    string AccountName,
    decimal Balance);

public sealed record DashboardCategoryTotalModel(
    Guid? CategoryId,
    string CategoryName,
    decimal Amount);

public sealed record DashboardMonthlyTotalModel(
    DateOnly Month,
    decimal Income,
    decimal Expenses)
{
    public string MonthLabel => Month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public decimal Net => Income - Expenses;
}

public sealed record BudgetModel(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    DateOnly Month,
    decimal Amount,
    decimal Spent,
    decimal Remaining,
    decimal PercentUsed,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public string MonthLabel => Month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public bool IsOverBudget => Spent > Amount;
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record AccountInput(string Name, AccountType Type);

public sealed record CategoryInput(string Name, CategoryKind Kind);

public sealed record TransactionInput(
    Guid AccountId,
    Guid CategoryId,
    TransactionType Type,
    decimal Amount,
    DateOnly Date,
    string? Description);

public sealed record TransferInput(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateOnly Date,
    string? Description);

public sealed record BudgetInput(
    Guid CategoryId,
    DateOnly Month,
    decimal Amount);

public sealed record BudgetUpdateInput(decimal Amount);

public sealed record UserModel(
    Guid Id,
    string Username,
    string Email,
    string DisplayName);

public sealed record LoginResultModel(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserModel User);

public sealed record LoginInput(string Identifier, string Password);

public sealed record RegisterInput(
    string Username,
    string? Email,
    string Password,
    string InviteCode,
    string? DisplayName);

public sealed record RegisterResultModel(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserModel User,
    bool EmailGenerated);

public sealed record ChangePasswordInput(string CurrentPassword, string NewPassword);
