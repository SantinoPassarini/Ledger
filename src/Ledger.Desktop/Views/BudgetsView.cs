using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Ledger.Desktop.Api;
using Ledger.Desktop.Dialogs;
using Ledger.Desktop.Models;
using Ledger.Desktop.Ui;

namespace Ledger.Desktop.Views;

public sealed class BudgetsView : ApiViewBase
{
    private readonly DataGridView _grid;
    private readonly FlowLayoutPanel _toolbar;
    private readonly Label _monthLabel;
    private readonly Button _previousMonthButton;
    private readonly Button _nextMonthButton;
    private readonly Button _currentMonthButton;
    private readonly Button _newButton;
    private readonly Button _editButton;
    private readonly Button _archiveButton;
    private readonly Button _refreshButton;
    private readonly CheckBox _showArchived;

    private DateOnly _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private List<CategoryModel> _categories = new();

    public BudgetsView(LedgerApiClient api, Action<string> setStatus)
        : base(api, setStatus)
    {
        Dock = DockStyle.Fill;

        _grid = UiFactory.CreateGrid();
        _grid.Name = "BudgetsGrid";
        _grid.Columns.AddRange(
            UiFactory.CreateColumn(nameof(BudgetModel.CategoryName), "Category", 28),
            UiFactory.CreateColumn(nameof(BudgetModel.MonthLabel), "Month", 12),
            UiFactory.CreateColumn(
                nameof(BudgetModel.Amount),
                "Amount",
                14,
                "N2",
                DataGridViewContentAlignment.MiddleRight),
            UiFactory.CreateColumn(
                nameof(BudgetModel.Spent),
                "Spent",
                14,
                "N2",
                DataGridViewContentAlignment.MiddleRight),
            UiFactory.CreateColumn(
                nameof(BudgetModel.Remaining),
                "Remaining",
                14,
                "N2",
                DataGridViewContentAlignment.MiddleRight),
            UiFactory.CreateColumn(
                nameof(BudgetModel.PercentUsed),
                "Used",
                12,
                "0.00'%'",
                DataGridViewContentAlignment.MiddleRight),
            UiFactory.CreateColumn(
                nameof(BudgetModel.IsArchived),
                "Archived",
                6,
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

        _monthLabel = new Label
        {
            Name = "BudgetsMonthLabel",
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Margin = new Padding(4, 8, 8, 0),
            Text = _month.ToString("MMMM yyyy", CultureInfo.CurrentCulture)
        };

        _previousMonthButton = UiFactory.CreateButton("<");
        _previousMonthButton.Click += async (_, _) => await RunAsync(() => ChangeMonthCoreAsync(-1));

        _nextMonthButton = UiFactory.CreateButton(">");
        _nextMonthButton.Click += async (_, _) => await RunAsync(() => ChangeMonthCoreAsync(1));

        _currentMonthButton = UiFactory.CreateButton("This month");
        _currentMonthButton.Click += async (_, _) => await RunAsync(GoToCurrentMonthCoreAsync);

        _newButton = UiFactory.CreateButton("New");
        _newButton.Click += async (_, _) => await RunAsync(CreateCoreAsync);

        _editButton = UiFactory.CreateButton("Edit");
        _editButton.Click += async (_, _) => await RunAsync(EditSelectedCoreAsync);

        _archiveButton = UiFactory.CreateButton("Archive");
        _archiveButton.Click += async (_, _) => await RunAsync(ArchiveSelectedCoreAsync);

        _refreshButton = UiFactory.CreateButton("Refresh");
        _refreshButton.Click += async (_, _) => await RunAsync(ReloadCoreAsync);

        _showArchived = UiFactory.CreateCheckBox("Show archived");
        _showArchived.CheckedChanged += async (_, _) => await RunAsync(ReloadCoreAsync);

        _toolbar = UiFactory.CreateToolbar();
        _toolbar.Controls.AddRange(
        [
            UiFactory.CreateLabel("Month"),
            _previousMonthButton,
            _monthLabel,
            _nextMonthButton,
            _currentMonthButton,
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
        var selected = SelectedBudget();

        _newButton.Enabled = !IsBusy;
        _refreshButton.Enabled = !IsBusy;
        _editButton.Enabled = !IsBusy && selected is { IsArchived: false };
        _archiveButton.Enabled = !IsBusy && selected is { IsArchived: false };
    }

    private BudgetModel? SelectedBudget()
    {
        return _grid.CurrentRow?.DataBoundItem as BudgetModel;
    }

    private async Task ChangeMonthCoreAsync(int offset)
    {
        _month = _month.AddMonths(offset);
        await ReloadCoreAsync();
    }

    private async Task GoToCurrentMonthCoreAsync()
    {
        _month = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        await ReloadCoreAsync();
    }

    private async Task ReloadCoreAsync()
    {
        var selectedId = SelectedBudget()?.Id;
        var budgets = await Api.GetBudgetsAsync(_month, _showArchived.Checked);
        var categories = await Api.GetCategoriesAsync(CategoryKind.Expense);

        _categories = categories
            .Where(category => !category.IsArchived)
            .OrderBy(category => category.Name)
            .ToList();

        _monthLabel.Text = _month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        _grid.DataSource = budgets.ToList();

        if (selectedId is Guid id)
        {
            SelectRow(id);
        }

        SetStatus($"Loaded {budgets.Count} budget{(budgets.Count == 1 ? string.Empty : "s")}.");
    }

    private void SelectRow(Guid id)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.DataBoundItem is BudgetModel budget && budget.Id == id)
            {
                row.Selected = true;
                _grid.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    private async Task CreateCoreAsync()
    {
        var month = _month;

        while (true)
        {
            using var dialog = new BudgetDialog("New budget", _categories, month);

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await Api.CreateBudgetAsync(dialog.Input);
                SetStatus("Budget created.");
                await ReloadCoreAsync();
                return;
            }
            catch (Exception exception) when (
                exception is ApiException or HttpRequestException or TaskCanceledException)
            {
                UiMessages.ShowError(this, exception);
            }
        }
    }

    private async Task EditSelectedCoreAsync()
    {
        var budget = SelectedBudget();

        if (budget is null || budget.IsArchived)
        {
            return;
        }

        while (true)
        {
            using var dialog = new BudgetDialog("Edit budget", _categories, budget.Month, budget);

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await Api.UpdateBudgetAsync(
                    budget.Id,
                    new BudgetUpdateInput(dialog.Input.Amount));
                SetStatus("Budget updated.");
                await ReloadCoreAsync();
                return;
            }
            catch (Exception exception) when (
                exception is ApiException or HttpRequestException or TaskCanceledException)
            {
                UiMessages.ShowError(this, exception);
            }
        }
    }

    private async Task ArchiveSelectedCoreAsync()
    {
        var budget = SelectedBudget();

        if (budget is null || budget.IsArchived)
        {
            return;
        }

        if (!UiMessages.ConfirmArchive(
                this,
                $"budget \"{budget.CategoryName}\" for {budget.MonthLabel}"))
        {
            return;
        }

        await Api.ArchiveBudgetAsync(budget.Id);
        SetStatus("Budget archived.");
        await ReloadCoreAsync();
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 ||
            _grid.Rows[eventArgs.RowIndex].DataBoundItem is not BudgetModel budget)
        {
            return;
        }

        var style = eventArgs.CellStyle;

        if (style is null)
        {
            return;
        }

        style.ForeColor = budget.IsArchived
            ? SystemColors.GrayText
            : budget.IsOverBudget
                ? Color.FromArgb(185, 28, 28)
                : budget.PercentUsed >= 80m
                    ? Color.FromArgb(194, 120, 3)
                    : _grid.DefaultCellStyle.ForeColor;
    }
}
