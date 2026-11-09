namespace Ledger.Api.Contracts.Dashboard;

public sealed record DashboardMonthlyTotal(
    DateOnly Month,
    decimal Income,
    decimal Expenses);
