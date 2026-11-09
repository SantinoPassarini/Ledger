using System.ComponentModel.DataAnnotations;

namespace Ledger.Api.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(256)]
    public string? Email { get; set; }

    [Required]
    [MinLength(12)]
    [MaxLength(200)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string InviteCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DisplayName { get; set; }
}
