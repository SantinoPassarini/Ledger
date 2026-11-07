using System.Text.Json;
using Ledger.Api;
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

    private readonly string _schema = $"ledger_test_{Guid.NewGuid():N}";
    private string _baseConnectionString = string.Empty;
    private string _testConnectionString = string.Empty;
    private string? _previousConnectionString;

    public string Schema => _schema;

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

        using var client = CreateClient();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Ledger"] = _testConnectionString
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
}

[CollectionDefinition("api", DisableParallelization = true)]
public sealed class ApiCollection : ICollectionFixture<LedgerApiFactory>
{
}
