namespace Ledger.Api.Domain;

public sealed class Account
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public AccountType Type { get; set; }

    public string Currency { get; set; } = "ARS";

    public bool IsArchived { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
