namespace Ledger.Api.Contracts.Auth;

public sealed record RegisterResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserResponse User,
    bool EmailGenerated);
