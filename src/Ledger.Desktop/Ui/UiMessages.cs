using System.Windows.Forms;
using Ledger.Desktop.Api;

namespace Ledger.Desktop.Ui;

public static class UiMessages
{
    public static void ShowError(IWin32Window owner, Exception exception)
    {
        var message = exception switch
        {
            ApiException apiException => apiException.Message,
            HttpRequestException => "Could not reach the Ledger API. Make sure it is running and that the API URL is correct.",
            TaskCanceledException => "The request to the Ledger API timed out.",
            _ => exception.Message
        };

        MessageBox.Show(owner, message, "Ledger", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static void ShowInformation(IWin32Window owner, string message)
    {
        MessageBox.Show(owner, message, "Ledger", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static bool ConfirmArchive(IWin32Window owner, string description)
    {
        return MessageBox.Show(
            owner,
            $"Archive {description}?\n\nArchived records are hidden by default and cannot be modified.",
            "Ledger",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;
    }
}
