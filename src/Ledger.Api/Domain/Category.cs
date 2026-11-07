namespace Ledger.Api.Domain;

public sealed class Category
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public CategoryKind Kind { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
