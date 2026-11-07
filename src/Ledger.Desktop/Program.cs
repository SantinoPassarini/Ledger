using Ledger.Desktop.Api;
using Ledger.Desktop.Configuration;

namespace Ledger.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            var apiBaseUrl = AppSettings.ApiBaseUrl;

            using var api = new LedgerApiClient(apiBaseUrl);

            Application.Run(new MainForm(api, apiBaseUrl));
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Could not start Ledger.\n\n{exception.Message}",
                "Ledger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
