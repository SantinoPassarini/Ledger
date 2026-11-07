using Ledger.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ledger.Api.Tests;

[Collection("api")]
public sealed class IsolationTests : ApiTestBase
{
    public IsolationTests(LedgerApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Api_UsesTheIsolatedTestSchema()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        var connectionString = db.Database.GetConnectionString();

        Assert.NotNull(connectionString);
        Assert.True(
            connectionString.Contains(Factory.Schema, StringComparison.Ordinal),
            "The API is not using the test schema connection string.");

        await db.Database.OpenConnectionAsync();
        try
        {
            var connection = db.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT current_schema()";

            var schema = (string?)await command.ExecuteScalarAsync();
            Assert.Equal(Factory.Schema, schema);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
