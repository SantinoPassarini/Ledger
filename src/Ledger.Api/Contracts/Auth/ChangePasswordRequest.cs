using System.ComponentModel.DataAnnotations;

namespace Ledger.Api.Contracts.Auth;

public sealed class ChangePasswordRequest
{
    [Required]
    [MaxLength(200)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(12)]
    [MaxLength(200)]
    public string NewPassword { get; set; } = string.Empty;
}
