namespace Ledger.Api.Contracts.Dashboard;

public sealed record DashboardAccountBalance(
    Guid AccountId,
    string AccountName,
    decimal Balance);
