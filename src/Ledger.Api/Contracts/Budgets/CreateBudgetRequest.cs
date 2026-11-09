using System.ComponentModel.DataAnnotations;

namespace Ledger.Api.Contracts.Budgets;

public sealed class CreateBudgetRequest
{
    public Guid CategoryId { get; init; }

    [Required]
    public DateOnly? Month { get; init; }

    [Required]
    public decimal? Amount { get; init; }
}
