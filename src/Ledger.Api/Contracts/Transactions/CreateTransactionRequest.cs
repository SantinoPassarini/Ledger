using System.ComponentModel.DataAnnotations;
using Ledger.Api.Domain;

namespace Ledger.Api.Contracts.Transactions;

public sealed class CreateTransactionRequest
{
    public Guid AccountId { get; init; }

    public Guid CategoryId { get; init; }

    [Required]
    public TransactionType? Type { get; init; }

    [Required]
    public decimal? Amount { get; init; }

    [Required]
    public DateOnly? Date { get; init; }

    [MaxLength(200)]
    public string? Description { get; init; }
}
