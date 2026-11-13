using Ledger.Api.Data;
using Ledger.Api.Domain;
using Ledger.Api.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ledger.Api.Auth;

/// <summary>
/// Provisions the owner account plus any additional configured users at startup.
/// Credentials come from configuration (user secrets or environment variables) and are
/// never logged or committed.
/// </summary>
public sealed class UserProvisioningService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserProvisioningService> _logger;
    private readonly IHostEnvironment _environment;

    public UserProvisioningService(
        IServiceScopeFactory scopeFactory,
        ILogger<UserProvisioningService> logger,
        IHostEnvironment environment)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _environment = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ProvisionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Users could not be provisioned. Sign-in will not work until the API configuration is fixed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task ProvisionAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var options = services.GetRequiredService<IOptions<LedgerOptions>>().Value;

        var definitions = BuildDefinitions(options);

        if (definitions.Count == 0)
        {
            if (_environment.IsProduction())
            {
                _logger.LogInformation(
                    "No users are configured for provisioning. Existing database accounts are left unchanged.");
            }
            else
            {
                _logger.LogWarning(
                    "No Ledger users are configured. Sign-in is disabled.");
            }

            return;
        }

        var db = services.GetRequiredService<LedgerDbContext>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher<User>>();

        foreach (var definition in definitions)
        {
            try
            {
                await ProvisionUserAsync(db, passwordHasher, definition, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "User {Username} could not be provisioned.",
                    definition.Username);
            }
        }
    }

    private List<UserDefinition> BuildDefinitions(LedgerOptions options)
    {
        var definitions = new List<UserDefinition>();
        var normalizedUsernames = new HashSet<string>(StringComparer.Ordinal);

        var owner = options.Owner;

        if (!string.IsNullOrWhiteSpace(owner.Username) ||
            !string.IsNullOrWhiteSpace(owner.Email) ||
            !string.IsNullOrWhiteSpace(owner.Password))
        {
            definitions.Add(new UserDefinition(
                LedgerSeed.OwnerUserId,
                owner.Username,
                owner.Email,
                owner.Password,
                owner.DisplayName,
                "Ledger:Owner"));

            var ownerUsername = ResolveUsername(owner.Username, owner.Email);

            if (!string.IsNullOrWhiteSpace(ownerUsername))
            {
                normalizedUsernames.Add(User.NormalizeUsername(ownerUsername));
            }
        }
        else
        {
            _logger.LogWarning(
                "Ledger:Owner is not configured. The owner account cannot sign in.");
        }

        foreach (var pair in options.Users)
        {
            var username = string.IsNullOrWhiteSpace(pair.Value.Username)
                ? pair.Key
                : pair.Value.Username;
            var normalizedUsername = User.NormalizeUsername(username);

            if (!normalizedUsernames.Add(normalizedUsername))
            {
                _logger.LogWarning(
                    "Ledger:Users contains the duplicate username {Username}; it will be skipped.",
                    username);
                continue;
            }

            definitions.Add(new UserDefinition(
                null,
                username,
                pair.Value.Email,
                pair.Value.Password,
                pair.Value.DisplayName,
                $"Ledger:Users:{pair.Key}"));
        }

        return definitions;
    }

    private async Task ProvisionUserAsync(
        LedgerDbContext db,
        IPasswordHasher<User> passwordHasher,
        UserDefinition definition,
        CancellationToken cancellationToken)
    {
        var username = ResolveUsername(definition.Username, definition.Email);

        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogError(
                "{Section}:Username or {Section}:Email must be configured.",
                definition.Section,
                definition.Section);
            return;
        }

        if (username.Length > 64)
        {
            _logger.LogError(
                "{Section}:Username must have at most 64 characters.",
                definition.Section);
            return;
        }

        var email = string.IsNullOrWhiteSpace(definition.Email)
            ? $"{username}@ledger.local"
            : definition.Email.Trim();

        if (email.Length > 256)
        {
            _logger.LogError(
                "{Section}:Email must have at most 256 characters.",
                definition.Section);
            return;
        }

        if (string.IsNullOrWhiteSpace(definition.Password))
        {
            _logger.LogError(
                "{Section}:Password is not configured. Sign-in is disabled for {Username}.",
                definition.Section,
                username);
            return;
        }

        if (definition.Password.Length < 8)
        {
            _logger.LogError(
                "{Section}:Password must have at least 8 characters.",
                definition.Section);
            return;
        }

        var normalizedUsername = User.NormalizeUsername(username);
        var normalizedEmail = User.NormalizeEmail(email);
        var displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
            ? username
            : definition.DisplayName.Trim();

        if (displayName.Length > 100)
        {
            displayName = displayName[..100];
        }

        User? user = null;

        if (definition.UserId is Guid userId)
        {
            user = await db.Users
                .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        }

        user ??= await db.Users
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.NormalizedUsername == normalizedUsername ||
                    candidate.NormalizedEmail == normalizedEmail,
                cancellationToken);

        // In Production, provisioning is create-only. Existing accounts may have been
        // changed through the API (password, email, display name) and must not be reset by
        // a restart or by a stale environment variable.
        if (user is not null && _environment.IsProduction())
        {
            _logger.LogInformation(
                "User {Username} already exists. Production provisioning does not modify existing accounts.",
                user.Username);
            return;
        }

        if (user is null)
        {
            user = new User
            {
                Id = definition.UserId ?? Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Users.Add(user);
        }

        user.Username = username;
        user.NormalizedUsername = normalizedUsername;
        user.Email = email;
        user.NormalizedEmail = normalizedEmail;
        user.DisplayName = displayName;
        user.IsActive = true;

        var needsPasswordHash = string.IsNullOrWhiteSpace(user.PasswordHash);

        if (!needsPasswordHash)
        {
            var verification = passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                definition.Password);

            needsPasswordHash = verification == PasswordVerificationResult.Failed;
        }

        if (needsPasswordHash)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, definition.Password);
        }

        await db.SaveChangesAsync(cancellationToken);
        await EnsureDefaultCategoriesAsync(db, user.Id, cancellationToken);

        _logger.LogInformation("User {Username} is ready.", user.Username);
    }

    private static async Task EnsureDefaultCategoriesAsync(
        LedgerDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var hasCategories = await db.Categories
            .IgnoreQueryFilters()
            .AnyAsync(category => category.UserId == userId, cancellationToken);

        if (hasCategories)
        {
            return;
        }

        UserAccountFactory.AddDefaultCategories(db, userId);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string ResolveUsername(string? username, string? email)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            return username.Trim();
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var atIndex = email.IndexOf('@');
            var localPart = atIndex > 0 ? email[..atIndex] : email;
            return localPart.Trim();
        }

        return string.Empty;
    }

    private sealed record UserDefinition(
        Guid? UserId,
        string Username,
        string Email,
        string Password,
        string DisplayName,
        string Section);
}
