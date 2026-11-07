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
    Guid CategoryId,
    string CategoryName,
    TransactionType Type,
    decimal Amount,
    DateOnly Date,
    string? Description,
    Guid? TransferGroupId,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

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
