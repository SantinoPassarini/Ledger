namespace Ledger.Api.Contracts.Auth;

public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    string DisplayName);
