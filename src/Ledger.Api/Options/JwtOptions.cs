namespace Ledger.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Ledger.Api";

    public string Audience { get; set; } = "Ledger.Desktop";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 480;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Issuer) &&
        !string.IsNullOrWhiteSpace(Audience) &&
        SigningKey.Length >= 32 &&
        AccessTokenMinutes > 0;
}
