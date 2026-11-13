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
    /// Self-registration is disabled by default and is intended only for a private
    /// instance reachable by users authorized to create an account.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Shared invite code required by the registration endpoint. It is read from user
    /// secrets or an environment variable and must not be committed.
    /// </summary>
    public string InviteCode { get; set; } = string.Empty;
}
