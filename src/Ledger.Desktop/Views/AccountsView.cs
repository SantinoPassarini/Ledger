using System.Drawing;
using System.Windows.Forms;
using Ledger.Desktop.Api;
using Ledger.Desktop.Dialogs;
using Ledger.Desktop.Models;
using Ledger.Desktop.Ui;

namespace Ledger.Desktop.Views;

public sealed class AccountsView : ApiViewBase
{
    private readonly DataGridView _grid;
    private readonly FlowLayoutPanel _toolbar;
    private readonly CheckBox _showArchived;
    private readonly Button _newButton;
    private readonly Button _editButton;
    private readonly Button _archiveButton;
    private readonly Button _refreshButton;

    public AccountsView(LedgerApiClient api, Action<string> setStatus)
        : base(api, setStatus)
    {
        Dock = DockStyle.Fill;

        _grid = UiFactory.CreateGrid();
        _grid.Columns.AddRange(
            UiFactory.CreateColumn(nameof(AccountModel.Name), "Name", 42),
            UiFactory.CreateColumn(nameof(AccountModel.Type), "Type", 16),
            UiFactory.CreateColumn(nameof(AccountModel.Currency), "Currency", 12),
            UiFactory.CreateColumn(
                nameof(AccountModel.Balance),
                "Balance",
                20,
                "N2",
                DataGridViewContentAlignment.MiddleRight),
            UiFactory.CreateColumn(
                nameof(AccountModel.IsArchived),
                "Archived",
                10,
                alignment: DataGridViewContentAlignment.MiddleCenter));
        _grid.CellFormatting += OnCellFormatting;
        _grid.SelectionChanged += (_, _) => UpdateButtons();
        _grid.CellDoubleClick += async (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0)
            {
                await RunAsync(EditSelectedCoreAsync);
            }
        };

        _showArchived = UiFactory.CreateCheckBox("Show archived");
        _showArchived.CheckedChanged += async (_, _) => await RunAsync(ReloadCoreAsync);

        _newButton = UiFactory.CreateButton("New");
        _newButton.Click += async (_, _) => await RunAsync(CreateCoreAsync);

        _editButton = UiFactory.CreateButton("Edit");
        _editButton.Click += async (_, _) => await RunAsync(EditSelectedCoreAsync);

        _archiveButton = UiFactory.CreateButton("Archive");
        _archiveButton.Click += async (_, _) => await RunAsync(ArchiveSelectedCoreAsync);

        _refreshButton = UiFactory.CreateButton("Refresh");
        _refreshButton.Click += async (_, _) => await RunAsync(ReloadCoreAsync);

        _toolbar = UiFactory.CreateToolbar();
        _toolbar.Controls.AddRange(
        [
            _newButton,
            _editButton,
            _archiveButton,
            _refreshButton,
            _showArchived
        ]);

        Controls.Add(_grid);
        Controls.Add(_toolbar);

        UpdateButtons();
    }

    public override Task RefreshAsync()
    {
        return RunAsync(ReloadCoreAsync);
    }

    protected override void SetInteractionEnabled(bool enabled)
    {
        _toolbar.Enabled = enabled;
        _grid.Enabled = enabled;
        UseWaitCursor = !enabled;
    }

    protected override void UpdateButtons()
    {
        var selected = SelectedAccount();

        _newButton.Enabled = !IsBusy;
        _refreshButton.Enabled = !IsBusy;
        _editButton.Enabled = !IsBusy && selected is { IsArchived: false };
        _archiveButton.Enabled = !IsBusy && selected is { IsArchived: false };
    }

    private AccountModel? SelectedAccount()
    {
        return _grid.CurrentRow?.DataBoundItem as AccountModel;
    }

    private async Task ReloadCoreAsync()
    {
        var selectedId = SelectedAccount()?.Id;
        var accounts = await Api.GetAccountsAsync(_showArchived.Checked);

        _grid.DataSource = accounts.ToList();

        if (selectedId is Guid id)
        {
            SelectRow(id);
        }

        SetStatus($"Loaded {accounts.Count} account{(accounts.Count == 1 ? string.Empty : "s")}.");
    }

    private void SelectRow(Guid id)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.DataBoundItem is AccountModel account && account.Id == id)
            {
                row.Selected = true;
                _grid.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    private async Task CreateCoreAsync()
    {
        var name = string.Empty;
        var type = AccountType.Cash;

        while (true)
        {
            using var dialog = new AccountDialog("New account", name, type);

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await Api.CreateAccountAsync(new AccountInput(dialog.AccountName, dialog.SelectedType));
                SetStatus("Account created.");
                await ReloadCoreAsync();
                return;
            }
            catch (Exception exception) when (
                exception is ApiException or HttpRequestException or TaskCanceledException)
            {
                UiMessages.ShowError(this, exception);
                name = dialog.AccountName;
                type = dialog.SelectedType;
            }
        }
    }

    private async Task EditSelectedCoreAsync()
    {
        var account = SelectedAccount();

        if (account is null || account.IsArchived)
        {
            return;
        }

        var name = account.Name;
        var type = account.Type;

        while (true)
        {
            using var dialog = new AccountDialog("Edit account", name, type);

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await Api.UpdateAccountAsync(
                    account.Id,
                    new AccountInput(dialog.AccountName, dialog.SelectedType));
                SetStatus("Account updated.");
                await ReloadCoreAsync();
                return;
            }
            catch (Exception exception) when (
                exception is ApiException or HttpRequestException or TaskCanceledException)
            {
                UiMessages.ShowError(this, exception);
                name = dialog.AccountName;
                type = dialog.SelectedType;
            }
        }
    }

    private async Task ArchiveSelectedCoreAsync()
    {
        var account = SelectedAccount();

        if (account is null || account.IsArchived)
        {
            return;
        }

        if (!UiMessages.ConfirmArchive(this, $"account \"{account.Name}\""))
        {
            return;
        }

        await Api.ArchiveAccountAsync(account.Id);
        SetStatus("Account archived.");
        await ReloadCoreAsync();
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 ||
            _grid.Rows[eventArgs.RowIndex].DataBoundItem is not AccountModel account)
        {
            return;
        }

        var style = eventArgs.CellStyle;

        if (style is null)
        {
            return;
        }

        style.ForeColor = account.IsArchived
            ? SystemColors.GrayText
            : _grid.DefaultCellStyle.ForeColor;
    }
}
