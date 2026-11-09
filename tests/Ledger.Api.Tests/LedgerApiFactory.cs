using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Ledger.Api;
using Ledger.Api.Contracts.Auth;
using Ledger.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Ledger.Api.Tests;

public sealed class LedgerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ApiUserSecretsId = "82623afe-4824-474b-92f3-57dda12396de";

    public const string RegistrationInviteCode = "test-invite-code";

    private readonly string _signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private readonly string _schema = $"ledger_test_{Guid.NewGuid():N}";
    private string _baseConnectionString = string.Empty;
    private string _testConnectionString = string.Empty;
    private string? _previousConnectionString;

    public string Schema => _schema;

    public string AccessToken { get; private set; } = string.Empty;

    public string OwnerUsername { get; } = $"owner-{Guid.NewGuid():N}"[..20];

    public string OwnerEmail { get; } = $"owner-{Guid.NewGuid():N}@ledger.test";

    public string OwnerPassword { get; } =
        $"Pw-{Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))}";

    public string SecondUsername { get; } = $"second-{Guid.NewGuid():N}"[..21];

    public string SecondEmail { get; } = $"second-{Guid.NewGuid():N}@ledger.test";

    public string SecondPassword { get; } =
        $"Pw-{Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))}";

    public async Task InitializeAsync()
    {
        _baseConnectionString = ResolveBaseConnectionString();

        await using (var connection = new NpgsqlConnection(_baseConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE SCHEMA \"{_schema}\"";
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_baseConnectionString)
        {
            SearchPath = _schema
        };
        _testConnectionString = builder.ConnectionString;
        _previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Ledger");
        Environment.SetEnvironmentVariable("ConnectionStrings__Ledger", _testConnectionString);

        // The schema must exist and be migrated before the host starts, because the user
        // provisioning hosted service queries the Users table during startup.
        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        using (var bootstrap = CreateClient())
        {
            AccessToken = await LoginAsync(bootstrap, OwnerUsername, OwnerPassword);
        }
    }

    public HttpClient CreateUnauthenticatedClient()
    {
        return CreateClient();
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
        return client;
    }

    public async Task<string> LoginAsync(
        HttpClient client,
        string identifier,
        string password)
    {
        using var response = await client.PostAsJsonAsync(
            "api/v1/auth/login",
            new LoginRequest { Identifier = identifier, Password = password });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResult>();
        return body?.AccessToken
            ?? throw new InvalidOperationException("The login response did not contain an access token.");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Ledger"] = _testConnectionString,
                ["Jwt:Issuer"] = "Ledger.Api.Tests",
                ["Jwt:Audience"] = "Ledger.Desktop.Tests",
                ["Jwt:SigningKey"] = _signingKey,
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Ledger:Owner:Username"] = OwnerUsername,
                ["Ledger:Owner:Email"] = OwnerEmail,
                ["Ledger:Owner:Password"] = OwnerPassword,
                ["Ledger:Owner:DisplayName"] = "Test Owner",
                [$"Ledger:Users:{SecondUsername}:Email"] = SecondEmail,
                [$"Ledger:Users:{SecondUsername}:Password"] = SecondPassword,
                [$"Ledger:Users:{SecondUsername}:DisplayName"] = "Second User",
                ["Ledger:Registration:Enabled"] = "true",
                ["Ledger:Registration:InviteCode"] = RegistrationInviteCode
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LedgerDbContext>>();
            services.RemoveAll<LedgerDbContext>();
            services.AddDbContext<LedgerDbContext>(options =>
                options
                    .UseNpgsql(_testConnectionString, npgsql =>
                        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", _schema))
                    .AddInterceptors(new SearchPathInterceptor(_schema)));
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__Ledger", _previousConnectionString);

        if (string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_baseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE";
        await command.ExecuteNonQueryAsync();
    }

    private LedgerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(_testConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", _schema))
            .AddInterceptors(new SearchPathInterceptor(_schema))
            .Options;

        return new LedgerDbContext(options);
    }

    private static string ResolveBaseConnectionString()
    {
        var environmentValue = Environment.GetEnvironmentVariable("LEDGER_TEST_CONNECTION");

        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        var secretsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            ApiUserSecretsId,
            "secrets.json");

        if (File.Exists(secretsPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(secretsPath));

            if (document.RootElement.TryGetProperty("ConnectionStrings:Ledger", out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString()!;
            }
        }

        throw new InvalidOperationException(
            "No test database was configured. Set LEDGER_TEST_CONNECTION or configure the Ledger.Api user secret ConnectionStrings:Ledger.");
    }

    private sealed record LoginResult(string AccessToken);
}

[CollectionDefinition("api", DisableParallelization = true)]
public sealed class ApiCollection : ICollectionFixture<LedgerApiFactory>
{
}
