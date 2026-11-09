namespace Ledger.Api.Domain;

public sealed class Transaction
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public Guid? CategoryId { get; set; }

    public Category? Category { get; set; }

    public TransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public DateOnly Date { get; set; }

    public string? Description { get; set; }

    public Guid? TransferGroupId { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
