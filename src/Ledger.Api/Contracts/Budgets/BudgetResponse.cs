namespace Ledger.Api.Contracts.Budgets;

public sealed record BudgetResponse(
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
    DateTime UpdatedAtUtc);
