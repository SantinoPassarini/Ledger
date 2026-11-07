using Ledger.Api.Domain;

namespace Ledger.Api.Contracts.Transactions;

public sealed record TransactionResponse(
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
