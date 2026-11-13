namespace Ledger.Api.Tests;

public sealed class TestConnectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateTestConnection_RejectsMissingValue(string? value)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => LedgerApiFactory.ValidateTestConnection(value));

        Assert.Contains("LEDGER_TEST_CONNECTION is required", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("postgresql://user:password@ep-example-123456.us-east-2.aws.neon.tech/ledger?sslmode=require")]
    [InlineData("Host=ep-example-123456.us-east-2.aws.neon.tech;Database=ledger;Username=user;Password=password;SSL Mode=Require")]
    public void ValidateTestConnection_RejectsNeon(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => LedgerApiFactory.ValidateTestConnection(value));

        Assert.Contains("points to Neon", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateTestConnection_AcceptsDisposableDatabase()
    {
        const string value =
            "Host=localhost;Port=5432;Database=ledger_test;Username=postgres;Password=postgres";

        Assert.Equal(value, LedgerApiFactory.ValidateTestConnection(value));
    }
}
