namespace Ledger.Api.Contracts.Dashboard;

public sealed record DashboardCategoryTotal(
    Guid? CategoryId,
    string CategoryName,
    decimal Amount);
