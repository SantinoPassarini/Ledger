using Ledger.Api.Domain;

namespace Ledger.Api.Contracts.Accounts;

public sealed record AccountResponse(
    Guid Id,
    string Name,
    AccountType Type,
    string Currency,
    bool IsArchived,
    decimal Balance,
    DateTime CreatedAtUtc);
