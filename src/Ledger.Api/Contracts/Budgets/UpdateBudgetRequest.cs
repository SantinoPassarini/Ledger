using System.ComponentModel.DataAnnotations;

namespace Ledger.Api.Contracts.Budgets;

public sealed class UpdateBudgetRequest
{
    [Required]
    public decimal? Amount { get; init; }
}
