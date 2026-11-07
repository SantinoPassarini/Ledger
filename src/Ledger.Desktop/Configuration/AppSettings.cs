using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ledger.Desktop.Configuration;

public static class AppSettings
{
    private const string DefaultApiBaseUrl = "http://localhost:5100/";

    public static string ApiBaseUrl { get; } = LoadApiBaseUrl();

    private static string LoadApiBaseUrl()
    {
        var environmentValue = Environment.GetEnvironmentVariable("LEDGER_API_BASE_URL");

        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            if (!File.Exists(path))
            {
                return DefaultApiBaseUrl;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));

            if (document.RootElement.TryGetProperty("ApiBaseUrl", out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                var url = value.GetString();

                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        return DefaultApiBaseUrl;
    }
}
