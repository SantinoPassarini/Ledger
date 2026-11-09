using System.ComponentModel.DataAnnotations;

namespace Ledger.Api.Contracts.Auth;

public sealed class LoginRequest
{
    [Required]
    [MaxLength(256)]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Password { get; set; } = string.Empty;
}
