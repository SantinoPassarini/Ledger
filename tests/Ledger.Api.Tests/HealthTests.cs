using System.Net;

namespace Ledger.Api.Tests;

[Collection("api")]
public sealed class HealthTests : ApiTestBase
{
    public HealthTests(LedgerApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        using var response = await Client.GetAsync("health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<HealthResponse>(response);
        Assert.Equal("Healthy", body.Status);
        Assert.Equal("Reachable", body.Database);
    }

    private sealed record HealthResponse(string Status, string Database);
}
