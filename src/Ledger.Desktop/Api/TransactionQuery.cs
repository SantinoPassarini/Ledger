using Ledger.Desktop.Models;

namespace Ledger.Desktop.Api;

public sealed record TransactionQuery
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public Guid? AccountId { get; init; }

    public Guid? CategoryId { get; init; }

    public TransactionType? Type { get; init; }

    public string? Search { get; init; }

    public bool IncludeArchived { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}
