using Ledger.Api.Contracts.Transactions;

namespace Ledger.Api.Contracts.Dashboard;

public sealed record DashboardResponse(
    DateOnly Month,
    string Currency,
    decimal TotalBalance,
    decimal MonthIncome,
    decimal MonthExpenses,
    IReadOnlyList<DashboardAccountBalance> AccountBalances,
    IReadOnlyList<DashboardCategoryTotal> ExpensesByCategory,
    IReadOnlyList<DashboardMonthlyTotal> MonthlyTrend,
    IReadOnlyList<TransactionResponse> RecentTransactions);
