using System.ComponentModel.DataAnnotations;
using Ledger.Api.Domain;

namespace Ledger.Api.Contracts.Categories;

public sealed class UpdateCategoryRequest
{
    [Required]
    [MaxLength(80)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public CategoryKind? Kind { get; init; }
}
