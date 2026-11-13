using System.Text;
using Ledger.Api.Options;
using Microsoft.Extensions.Configuration;

namespace Ledger.Api.Configuration;

/// <summary>
/// Validates the configuration that must be present before the API accepts traffic in
/// Production. Fails fast instead of starting with an insecure or unusable configuration.
/// </summary>
public static class ProductionConfigurationValidator
{
    public static void Validate(IConfiguration configuration)
    {
        var errors = new List<string>();

        var jwt = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwt.Issuer))
        {
            errors.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(jwt.Audience))
        {
            errors.Add("Jwt:Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(jwt.SigningKey) ||
            Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
        {
            errors.Add("Jwt:SigningKey is required and must contain at least 32 characters.");
        }

        if (jwt.AccessTokenMinutes <= 0)
        {
            errors.Add("Jwt:AccessTokenMinutes must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Ledger")))
        {
            errors.Add("ConnectionStrings:Ledger is required.");
        }

        var registration = configuration
            .GetSection($"{LedgerOptions.SectionName}:Registration")
            .Get<RegistrationOptions>() ?? new RegistrationOptions();

        if (registration.Enabled && string.IsNullOrWhiteSpace(registration.InviteCode))
        {
            errors.Add(
                "Ledger:Registration:InviteCode is required when registration is enabled.");
        }

        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Production configuration is invalid:" +
            Environment.NewLine +
            "- " +
            string.Join(Environment.NewLine + "- ", errors));
    }
}
