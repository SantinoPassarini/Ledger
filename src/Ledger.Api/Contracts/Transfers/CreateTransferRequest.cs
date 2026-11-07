using System.ComponentModel.DataAnnotations;

namespace Ledger.Api.Contracts.Transfers;

public sealed class CreateTransferRequest
{
    public Guid FromAccountId { get; init; }

    public Guid ToAccountId { get; init; }

    [Required]
    public decimal? Amount { get; init; }

    [Required]
    public DateOnly? Date { get; init; }

    [MaxLength(200)]
    public string? Description { get; init; }
}
