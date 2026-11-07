using System.Drawing;
using System.Windows.Forms;
using Ledger.Desktop.Api;
using Ledger.Desktop.Dialogs;
using Ledger.Desktop.Models;
using Ledger.Desktop.Ui;

namespace Ledger.Desktop.Views;

public sealed class TransactionsView : ApiViewBase
{
    private readonly DataGridView _grid;
    private readonly FlowLayoutPanel _filterPanel;
    private readonly FlowLayoutPanel _actionPanel;
    private readonly DateTimePicker _fromPicker;
    private readonly DateTimePicker _toPicker;
    private readonly ComboBox _accountFilter;
    private readonly ComboBox _categoryFilter;
    private readonly ComboBox _typeFilter;
    private readonly TextBox _searchBox;
    private readonly CheckBox _includeArchived;
    private readonly Button _applyButton;
    private readonly Button _clearButton;
    private readonly Button _newButton;
    private readonly Button _transferButton;
    private readonly Button _editButton;
    private readonly Button _archiveButton;
    private readonly Button _refreshButton;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Label _pageLabel;
    private readonly ComboBox _pageSizeCombo;

    private IReadOnlyList<AccountModel> _allAccounts = Array.Empty<AccountModel>();
    private IReadOnlyList<CategoryModel> _allCategories = Array.Empty<CategoryModel>();
    private int _page = 1;
    private int _pageSize = 50;
    private int _totalPages;
    private int _totalCount;
    private bool _suppressFilterEvents;

    public TransactionsView(LedgerApiClient api, Action<string> setStatus)
        : base(api, setStatus)
    {
        Dock = DockStyle.Fill;

        _grid = UiFactory.CreateGrid();
        _grid.Columns.AddRange(
            UiFactory.CreateColumn(
                nameof(TransactionRow.Date),
                "Date",
                12,
                "yyyy-MM-dd",
                DataGridViewContentAlignment.MiddleCenter),
            UiFactory.CreateColumn(nameof(TransactionRow.Type), "Type", 10),
            UiFactory.CreateColumn(nameof(TransactionRow.Account), "Account", 17),
            UiFactory.CreateColumn(nameof(TransactionRow.Category), "Category", 17),
            UiFactory.CreateColumn(
                nameof(TransactionRow.Amount),
                "Amount",
                13,
                "+#,##0.00;-#,##0.00;0.00",
                DataGridViewContentAlignment.MiddleRight),
            UiFactory.CreateColumn(nameof(TransactionRow.Description), "Description", 25),
            UiFactory.CreateColumn(
                nameof(TransactionRow.IsArchived),
                "Archived",
                8,
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

        _fromPicker = UiFactory.CreateDatePicker(optional: true);
        _toPicker = UiFactory.CreateDatePicker(optional: true);

        _accountFilter = UiFactory.CreateComboBox(170);
        _accountFilter.DataSource = new List<FilterOption>
        {
            new(Guid.Empty, "All accounts")
        };
        _accountFilter.DisplayMember = nameof(FilterOption.Label);

        _categoryFilter = UiFactory.CreateComboBox(170);
        _categoryFilter.DataSource = new List<FilterOption>
        {
            new(Guid.Empty, "All categories")
        };
        _categoryFilter.DisplayMember = nameof(FilterOption.Label);

        _typeFilter = UiFactory.CreateComboBox(120);
        _typeFilter.DataSource = new[]
        {
            new TypeFilterOption("All types", null),
            new TypeFilterOption("Income", TransactionType.Income),
            new TypeFilterOption("Expense", TransactionType.Expense),
            new TypeFilterOption("Transfer out", TransactionType.TransferOut),
            new TypeFilterOption("Transfer in", TransactionType.TransferIn)
        };
        _typeFilter.DisplayMember = nameof(TypeFilterOption.Label);

        _searchBox = new TextBox
        {
            Width = 190,
            PlaceholderText = "Description",
            Margin = new Padding(0, 4, 8, 0)
        };
        _searchBox.KeyDown += async (_, eventArgs) =>
        {
            if (eventArgs.KeyCode == Keys.Enter)
            {
                eventArgs.SuppressKeyPress = true;
                await ApplyFiltersAsync();
            }
        };

        _includeArchived = UiFactory.CreateCheckBox("Include archived");

        _applyButton = UiFactory.CreateButton("Apply");
        _applyButton.Click += async (_, _) => await ApplyFiltersAsync();

        _clearButton = UiFactory.CreateButton("Clear");
        _clearButton.Click += async (_, _) => await ClearFiltersAsync();

        _filterPanel = UiFactory.CreateToolbar();
        _filterPanel.Controls.AddRange(
        [
            UiFactory.CreateLabel("From"),
            _fromPicker,
            UiFactory.CreateLabel("To"),
            _toPicker,
            UiFactory.CreateLabel("Account"),
            _accountFilter,
            UiFactory.CreateLabel("Category"),
            _categoryFilter,
            UiFactory.CreateLabel("Type"),
            _typeFilter,
            UiFactory.CreateLabel("Search"),
            _searchBox,
            _includeArchived,
            _applyButton,
            _clearButton
        ]);

        _newButton = UiFactory.CreateButton("New");
        _newButton.Click += async (_, _) => await RunAsync(CreateCoreAsync);

        _transferButton = UiFactory.CreateButton("Transfer");
        _transferButton.Click += async (_, _) => await RunAsync(CreateTransferCoreAsync);

        _editButton = UiFactory.CreateButton("Edit");
        _editButton.Click += async (_, _) => await RunAsync(EditSelectedCoreAsync);

        _archiveButton = UiFactory.CreateButton("Archive");
        _archiveButton.Click += async (_, _) => await RunAsync(ArchiveSelectedCoreAsync);

        _refreshButton = UiFactory.CreateButton("Refresh");
        _refreshButton.Click += async (_, _) => await RunAsync(ReloadCoreAsync);

        _previousButton = UiFactory.CreateButton("Previous");
        _previousButton.Click += async (_, _) => await GoToPreviousPageAsync();

        _nextButton = UiFactory.CreateButton("Next");
        _nextButton.Click += async (_, _) => await GoToNextPageAsync();

        _pageLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(8, 10, 8, 0),
            Text = "No transactions"
        };

        _pageSizeCombo = UiFactory.CreateComboBox(80);
        _pageSizeCombo.DataSource = new[] { 25, 50, 100 };
        _pageSizeCombo.SelectedItem = 50;
        _pageSizeCombo.SelectedIndexChanged += async (_, _) =>
        {
            if (_suppressFilterEvents || _pageSizeCombo.SelectedItem is not int pageSize)
            {
                return;
            }

            _pageSize = pageSize;
            _page = 1;
            await RunAsync(ReloadCoreAsync);
        };

        _actionPanel = UiFactory.CreateToolbar();
        _actionPanel.Controls.AddRange(
        [
            _newButton,
            _transferButton,
            _editButton,
            _archiveButton,
            _refreshButton,
            _previousButton,
            _pageLabel,
            _nextButton,
            UiFactory.CreateLabel("Page size"),
            _pageSizeCombo
        ]);

        Controls.Add(_grid);
        Controls.Add(_actionPanel);
        Controls.Add(_filterPanel);

        UpdateButtons();
    }

    public override Task RefreshAsync()
    {
        return RunAsync(ReloadCoreAsync);
    }

    protected override void SetInteractionEnabled(bool enabled)
    {
        _filterPanel.Enabled = enabled;
        _actionPanel.Enabled = enabled;
        _grid.Enabled = enabled;
        UseWaitCursor = !enabled;
    }

    protected override void UpdateButtons()
    {
        var selected = SelectedTransaction();

        _newButton.Enabled = !IsBusy;
        _transferButton.Enabled = !IsBusy;
        _refreshButton.Enabled = !IsBusy;
        _editButton.Enabled = !IsBusy &&
                              selected is { IsArchived: false } &&
                              selected.Type is TransactionType.Income or TransactionType.Expense;
        _archiveButton.Enabled = !IsBusy && selected is { IsArchived: false };
        _previousButton.Enabled = !IsBusy && _page > 1;
        _nextButton.Enabled = !IsBusy && _page < _totalPages;
    }

    private TransactionModel? SelectedTransaction()
    {
        return (_grid.CurrentRow?.DataBoundItem as TransactionRow)?.Model;
    }

    private async Task ReloadCoreAsync()
    {
        var accountsTask = Api.GetAccountsAsync(includeArchived: true);
        var categoriesTask = Api.GetCategoriesAsync(includeArchived: true);
        var pageTask = Api.GetTransactionsAsync(BuildQuery());

        await Task.WhenAll(accountsTask, categoriesTask, pageTask);

        _allAccounts = await accountsTask;
        _allCategories = await categoriesTask;

        UpdateLookups();
        await ApplyPageAsync(await pageTask);

        SetStatus(_totalCount == 0
            ? "No transactions found."
            : $"Loaded {_totalCount} transaction{(_totalCount == 1 ? string.Empty : "s")}.");
    }

    private void UpdateLookups()
    {
        var selectedAccountId = (_accountFilter.SelectedItem as FilterOption)?.Id ?? Guid.Empty;
        var selectedCategoryId = (_categoryFilter.SelectedItem as FilterOption)?.Id ?? Guid.Empty;

        var accountOptions = new List<FilterOption>
        {
            new(Guid.Empty, "All accounts")
        };
        accountOptions.AddRange(
            _allAccounts.Select(account => new FilterOption(
                account.Id,
                account.IsArchived ? $"{account.Name} (archived)" : account.Name)));

        var categoryOptions = new List<FilterOption>
        {
            new(Guid.Empty, "All categories")
        };
        categoryOptions.AddRange(
            _allCategories.Select(category => new FilterOption(
                category.Id,
                category.IsArchived ? $"{category.Name} (archived)" : category.Name)));

        _suppressFilterEvents = true;

        try
        {
            _accountFilter.DataSource = accountOptions;
            _accountFilter.DisplayMember = nameof(FilterOption.Label);
            SelectFilterOption(_accountFilter, selectedAccountId);

            _categoryFilter.DataSource = categoryOptions;
            _categoryFilter.DisplayMember = nameof(FilterOption.Label);
            SelectFilterOption(_categoryFilter, selectedCategoryId);
        }
        finally
        {
            _suppressFilterEvents = false;
        }
    }

    private static void SelectFilterOption(ComboBox comboBox, Guid id)
    {
        for (var i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is FilterOption option && option.Id == id)
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private TransactionQuery BuildQuery()
    {
        var accountId = (_accountFilter.SelectedItem as FilterOption)?.Id ?? Guid.Empty;
        var categoryId = (_categoryFilter.SelectedItem as FilterOption)?.Id ?? Guid.Empty;

        return new TransactionQuery
        {
            From = _fromPicker.Checked ? DateOnly.FromDateTime(_fromPicker.Value) : null,
            To = _toPicker.Checked ? DateOnly.FromDateTime(_toPicker.Value) : null,
            AccountId = accountId == Guid.Empty ? null : accountId,
            CategoryId = categoryId == Guid.Empty ? null : categoryId,
            Type = (_typeFilter.SelectedItem as TypeFilterOption)?.Type,
            Search = _searchBox.Text,
            IncludeArchived = _includeArchived.Checked,
            Page = _page,
            PageSize = _pageSize
        };
    }

    private async Task ApplyPageAsync(PagedResponse<TransactionModel> page)
    {
        if (page.TotalCount > 0 && page.Items.Count == 0 && _page > 1)
        {
            _page = Math.Max(1, page.TotalPages);
            page = await Api.GetTransactionsAsync(BuildQuery());
        }

        _totalPages = page.TotalPages;
        _totalCount = page.TotalCount;
        _page = page.Page;

        var selectedId = SelectedTransaction()?.Id;
        _grid.DataSource = page.Items.Select(transaction => new TransactionRow(transaction)).ToList();

        if (selectedId is Guid id)
        {
            SelectRow(id);
        }

        _pageLabel.Text = _totalCount == 0
            ? "No transactions"
            : $"Page {_page} of {_totalPages} ({_totalCount} total)";
    }

    private void SelectRow(Guid id)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.DataBoundItem is TransactionRow transaction && transaction.Id == id)
            {
                row.Selected = true;
                _grid.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    private async Task ApplyFiltersAsync()
    {
        _page = 1;
        await RunAsync(ReloadCoreAsync);
    }

    private async Task ClearFiltersAsync()
    {
        _suppressFilterEvents = true;

        try
        {
            _fromPicker.Checked = false;
            _toPicker.Checked = false;
            _accountFilter.SelectedIndex = 0;
            _categoryFilter.SelectedIndex = 0;
            _typeFilter.SelectedIndex = 0;
            _searchBox.Clear();
            _includeArchived.Checked = false;
            _pageSizeCombo.SelectedItem = 50;
            _pageSize = 50;
            _page = 1;
        }
        finally
        {
            _suppressFilterEvents = false;
        }

        await RunAsync(ReloadCoreAsync);
    }

    private async Task GoToPreviousPageAsync()
    {
        if (_page <= 1)
        {
            return;
        }

        _page--;
        await RunAsync(ReloadCoreAsync);
    }

    private async Task GoToNextPageAsync()
    {
        if (_page >= _totalPages)
        {
            return;
        }

        _page++;
        await RunAsync(ReloadCoreAsync);
    }

    private async Task CreateCoreAsync()
    {
        var activeAccounts = _allAccounts.Where(account => !account.IsArchived).ToList();

        if (activeAccounts.Count == 0)
        {
            UiMessages.ShowInformation(this, "Create an account before adding transactions.");
            return;
        }

        var activeCategories = _allCategories.Where(category => !category.IsArchived).ToList();

        if (activeCategories.Count == 0)
        {
            UiMessages.ShowInformation(this, "Create a category before adding transactions.");
            return;
        }

        TransactionInput? pending = null;

        while (true)
        {
            using var dialog = new TransactionDialog(
                "New transaction",
                activeAccounts,
                activeCategories,
                pending);

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await Api.CreateTransactionAsync(dialog.Input);
                SetStatus("Transaction created.");
                _page = 1;
                await ReloadCoreAsync();
                return;
            }
            catch (Exception exception) when (
                exception is ApiException or HttpRequestException or TaskCanceledException)
            {
                UiMessages.ShowError(this, exception);
                pending = dialog.Input;
            }
        }
    }

    private async Task CreateTransferCoreAsync()
    {
        var activeAccounts = _allAccounts.Where(account => !account.IsArchived).ToList();

        if (activeAccounts.Count < 2)
        {
            UiMessages.ShowInformation(this, "Create at least two active accounts before adding a transfer.");
            return;
        }

        TransferInput? pending = null;

        while (true)
        {
            using var dialog = new TransferDialog(activeAccounts, pending);

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await Api.CreateTransferAsync(dialog.Input);
                SetStatus("Transfer created.");
                _page = 1;
                await ReloadCoreAsync();
                return;
            }
            catch (Exception exception) when (
                exception is ApiException or HttpRequestException or TaskCanceledException)
            {
                UiMessages.ShowError(this, exception);
                pending = dialog.Input;
            }
        }
    }

    private async Task EditSelectedCoreAsync()
    {
        var transaction = SelectedTransaction();

        if (transaction is null ||
            transaction.IsArchived ||
            transaction.Type is not TransactionType.Income and not TransactionType.Expense)
        {
            return;
        }

        var accounts = GetAccountsForEditing(transaction.AccountId);
        var categories = GetCategoriesForEditing(transaction.CategoryId!.Value);
        var pending = new TransactionInput(
            transaction.AccountId,
            transaction.CategoryId.Value,
            transaction.Type,
            transaction.Amount,
            transaction.Date,
            transaction.Description);

        while (true)
        {
            using var dialog = new TransactionDialog(
                "Edit transaction",
                accounts,
                categories,
                pending);

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await Api.UpdateTransactionAsync(transaction.Id, dialog.Input);
                SetStatus("Transaction updated.");
                await ReloadCoreAsync();
                return;
            }
            catch (Exception exception) when (
                exception is ApiException or HttpRequestException or TaskCanceledException)
            {
                UiMessages.ShowError(this, exception);
                pending = dialog.Input;
            }
        }
    }

    private async Task ArchiveSelectedCoreAsync()
    {
        var transaction = SelectedTransaction();

        if (transaction is null || transaction.IsArchived)
        {
            return;
        }

        var label = transaction.Type is TransactionType.TransferOut or TransactionType.TransferIn
            ? "transfer"
            : "transaction";

        if (!UiMessages.ConfirmArchive(
                this,
                $"{label} from {transaction.Date:yyyy-MM-dd} for {transaction.Amount:N2}"))
        {
            return;
        }

        if (transaction.TransferGroupId is Guid transferGroupId)
        {
            await Api.ArchiveTransferAsync(transferGroupId);
            SetStatus("Transfer archived.");
        }
        else
        {
            await Api.ArchiveTransactionAsync(transaction.Id);
            SetStatus("Transaction archived.");
        }

        await ReloadCoreAsync();
    }

    private List<AccountModel> GetAccountsForEditing(Guid accountId)
    {
        var accounts = _allAccounts.Where(account => !account.IsArchived).ToList();

        if (accounts.All(account => account.Id != accountId))
        {
            var existing = _allAccounts.FirstOrDefault(account => account.Id == accountId);

            if (existing is not null)
            {
                accounts.Add(existing with { Name = $"{existing.Name} (archived)" });
            }
        }

        return accounts;
    }

    private List<CategoryModel> GetCategoriesForEditing(Guid categoryId)
    {
        var categories = _allCategories.Where(category => !category.IsArchived).ToList();

        if (categories.All(category => category.Id != categoryId))
        {
            var existing = _allCategories.FirstOrDefault(category => category.Id == categoryId);

            if (existing is not null)
            {
                categories.Add(existing with { Name = $"{existing.Name} (archived)" });
            }
        }

        return categories;
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 ||
            _grid.Rows[eventArgs.RowIndex].DataBoundItem is not TransactionRow transaction)
        {
            return;
        }

        var style = eventArgs.CellStyle;

        if (style is null)
        {
            return;
        }

        if (transaction.IsArchived)
        {
            style.ForeColor = SystemColors.GrayText;
            return;
        }

        style.ForeColor = _grid.DefaultCellStyle.ForeColor;

        if (_grid.Columns[eventArgs.ColumnIndex].DataPropertyName == nameof(TransactionRow.Amount))
        {
            style.ForeColor = transaction.Amount >= 0
                ? Color.FromArgb(21, 128, 61)
                : Color.FromArgb(185, 28, 28);
        }
    }

    private sealed record TransactionRow(TransactionModel Model)
    {
        public Guid Id => Model.Id;

        public DateOnly Date => Model.Date;

        public TransactionType Type => Model.Type;

        public string Account => Model.AccountName;

        public string Category => Model.CategoryName ?? "Transfer";

        public decimal Amount => Model.Type is TransactionType.Income or TransactionType.TransferIn
            ? Model.Amount
            : -Model.Amount;

        public string Description => Model.Description ?? string.Empty;

        public bool IsArchived => Model.IsArchived;
    }

    private sealed record FilterOption(Guid Id, string Label);

    private sealed record TypeFilterOption(string Label, TransactionType? Type);
}
