using System.Windows.Forms;
using Ledger.Desktop.Api;
using Ledger.Desktop.Ui;

namespace Ledger.Desktop.Views;

public abstract class ApiViewBase : UserControl
{
    private bool _busy;

    protected ApiViewBase(LedgerApiClient api, Action<string> setStatus)
    {
        Api = api;
        SetStatus = setStatus;
    }

    protected LedgerApiClient Api { get; }

    protected Action<string> SetStatus { get; }

    protected bool IsBusy => _busy;

    private bool CanUpdateUi => !IsDisposed && !Disposing;

    public abstract Task RefreshAsync();

    protected async Task RunAsync(Func<Task> action)
    {
        if (_busy || IsDisposed)
        {
            return;
        }

        _busy = true;
        SetInteractionEnabled(false);

        try
        {
            await action();
        }
        catch (ApiException exception) when (exception.StatusCode == 401)
        {
            // The client raises SessionExpired and MainForm returns to the sign-in window.
            SetStatus("Session expired.");
        }
        catch (Exception exception)
        {
            if (CanUpdateUi)
            {
                UiMessages.ShowError(this, exception);
                SetStatus("Operation failed.");
            }
        }
        finally
        {
            _busy = false;

            if (CanUpdateUi)
            {
                SetInteractionEnabled(true);
                UpdateButtons();
            }
        }
    }

    protected abstract void SetInteractionEnabled(bool enabled);

    protected virtual void UpdateButtons()
    {
    }
}
