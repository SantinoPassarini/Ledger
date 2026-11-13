using Ledger.Api.Configuration;
using Microsoft.Extensions.Configuration;

namespace Ledger.Api.Tests;

public sealed class ProductionConfigurationTests
{
    [Fact]
    public void Validate_AcceptsCompleteProductionConfiguration()
    {
        var configuration = BuildConfiguration();

        ProductionConfigurationValidator.Validate(configuration);
    }

    [Fact]
    public void Validate_RejectsMissingSigningKey()
    {
        var configuration = BuildConfiguration(new()
        {
            ["Jwt:SigningKey"] = string.Empty
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.Validate(configuration));

        Assert.Contains("Jwt:SigningKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsShortSigningKey()
    {
        var configuration = BuildConfiguration(new()
        {
            ["Jwt:SigningKey"] = "too-short"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.Validate(configuration));

        Assert.Contains("at least 32 characters", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsMissingConnectionString()
    {
        var configuration = BuildConfiguration(new()
        {
            ["ConnectionStrings:Ledger"] = string.Empty
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.Validate(configuration));

        Assert.Contains("ConnectionStrings:Ledger", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsRegistrationWithoutInviteCode()
    {
        var configuration = BuildConfiguration(new()
        {
            ["Ledger:Registration:Enabled"] = "true",
            ["Ledger:Registration:InviteCode"] = string.Empty
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.Validate(configuration));

        Assert.Contains(
            "Ledger:Registration:InviteCode",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsRegistrationWithInviteCode()
    {
        var configuration = BuildConfiguration(new()
        {
            ["Ledger:Registration:Enabled"] = "true",
            ["Ledger:Registration:InviteCode"] = "test-invite-code"
        });

        ProductionConfigurationValidator.Validate(configuration);
    }

    private static IConfiguration BuildConfiguration(
        Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Ledger"] =
                "Host=localhost;Port=5432;Database=ledger_test;Username=postgres;Password=postgres",
            ["Jwt:Issuer"] = "Ledger.Api",
            ["Jwt:Audience"] = "Ledger.Desktop",
            ["Jwt:SigningKey"] = new string('k', 32),
            ["Jwt:AccessTokenMinutes"] = "480",
            ["Ledger:Registration:Enabled"] = "false",
            ["Ledger:Registration:InviteCode"] = string.Empty
        };

        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
