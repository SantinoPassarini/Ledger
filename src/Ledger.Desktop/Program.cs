using Ledger.Desktop.Api;
using Ledger.Desktop.Configuration;
using Ledger.Desktop.Dialogs;

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

            while (true)
            {
                using (var login = new LoginForm(api, apiBaseUrl))
                {
                    if (login.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }
                }

                using var mainForm = new MainForm(api, apiBaseUrl);
                Application.Run(mainForm);

                if (!mainForm.SignOutRequested)
                {
                    return;
                }

                if (mainForm.SessionExpired)
                {
                    MessageBox.Show(
                        "Your session expired. Please sign in again.",
                        "Ledger",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
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
