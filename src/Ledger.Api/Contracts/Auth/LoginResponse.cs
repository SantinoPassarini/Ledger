namespace Ledger.Api.Contracts.Auth;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserResponse User);
