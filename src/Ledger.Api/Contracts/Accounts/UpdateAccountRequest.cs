using System.ComponentModel.DataAnnotations;
using Ledger.Api.Domain;

namespace Ledger.Api.Contracts.Accounts;

public sealed class UpdateAccountRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public AccountType? Type { get; init; }
}
