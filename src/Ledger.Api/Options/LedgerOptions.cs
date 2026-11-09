namespace Ledger.Api.Options;

public sealed class LedgerOptions
{
    public const string SectionName = "Ledger";

    public string Currency { get; set; } = "ARS";

    public OwnerOptions Owner { get; set; } = new();

    public Dictionary<string, ProvisionedUserOptions> Users { get; set; } = new();

    public RegistrationOptions Registration { get; set; } = new();
}

public sealed class OwnerOptions
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = "Ledger Owner";
}

public sealed class ProvisionedUserOptions
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

public sealed class RegistrationOptions
{
    /// <summary>
    /// Self-registration is disabled by default. Enable it only for a private instance
    /// that is reachable by the people who should be allowed to create an account.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Shared invite code required by the registration endpoint. Store it in user secrets
    /// or an environment variable; never commit it.
    /// </summary>
    public string InviteCode { get; set; } = string.Empty;
}
