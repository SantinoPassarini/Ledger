using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Ledger.Desktop.Api;
using Ledger.Desktop.Models;
using Ledger.Desktop.Ui;

namespace Ledger.Desktop.Views;

public sealed class DashboardView : ApiViewBase
{
    private readonly FlowLayoutPanel _toolbar;
    private readonly TableLayoutPanel _cardsPanel;
    private readonly TableLayoutPanel _gridsPanel;
    private readonly Label _monthLabel;
    private readonly Label _totalBalanceValue;
    private readonly Label _incomeValue;
    private readonly Label _expensesValue;
    private readonly Button _previousMonthButton;
    private readonly Button _nextMonthButton;
    private readonly Button _currentMonthButton;
    private readonly Button _refreshButton;
    private readonly MonthlyBarChart _monthlyChart;
    private readonly DataGridView _categoriesGrid;
    private readonly DataGridView _trendGrid;
    private readonly DataGridView _recentGrid;

    private DateOnly _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public DashboardView(LedgerApiClient api, Action<string> setStatus)
        : base(api, setStatus)
    {
        Dock = DockStyle.Fill;

        _monthLabel = new Label
        {
            Name = "DashboardMonthLabel",
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

        _refreshButton = UiFactory.CreateButton("Refresh");
        _refreshButton.Click += async (_, _) => await RunAsync(ReloadCoreAsync);

        _toolbar = UiFactory.CreateToolbar();
        _toolbar.Controls.AddRange(
        [
            UiFactory.CreateLabel("Month"),
            _previousMonthButton,
            _monthLabel,
            _nextMonthButton,
            _currentMonthButton,
            _refreshButton
        ]);

        _cardsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 104,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(12, 8, 12, 4),
            BackColor = SystemColors.Control
        };
        _cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        _cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        _cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

        _totalBalanceValue = CreateCard(
            _cardsPanel,
            "Total balance",
            "DashboardTotalBalanceValue",
            Color.FromArgb(30, 41, 59),
            0);
        _incomeValue = CreateCard(
            _cardsPanel,
            "Income",
            "DashboardIncomeValue",
            Color.FromArgb(21, 128, 61),
            1);
        _expensesValue = CreateCard(
            _cardsPanel,
            "Expenses",
            "DashboardExpensesValue",
            Color.FromArgb(185, 28, 28),
            2);

        _monthlyChart = new MonthlyBarChart
        {
            Name = "DashboardMonthlyChart",
            Dock = DockStyle.Fill
        };

        _categoriesGrid = UiFactory.CreateGrid();
        _categoriesGrid.Name = "DashboardCategoriesGrid";
        _categoriesGrid.Columns.AddRange(
            UiFactory.CreateColumn(nameof(DashboardCategoryTotalModel.CategoryName), "Category", 60),
            UiFactory.CreateColumn(
                nameof(DashboardCategoryTotalModel.Amount),
                "Amount",
                40,
                "N2",
                DataGridViewContentAlignment.MiddleRight));

        _trendGrid = UiFactory.CreateGrid();
        _trendGrid.Name = "DashboardTrendGrid";
        _trendGrid.Columns.AddRange(
            UiFactory.CreateColumn(nameof(DashboardMonthlyTotalModel.MonthLabel), "Month", 25),
            UiFactory.CreateColumn(
                nameof(DashboardMonthlyTotalModel.Income),
                "Income",
                25,
                "N2",
                DataGridViewContentAlignment.MiddleRight),
            UiFactory.CreateColumn(
                nameof(DashboardMonthlyTotalModel.Expenses),
                "Expenses",
                25,
                "N2",
                DataGridViewContentAlignment.MiddleRight),
            UiFactory.CreateColumn(
                nameof(DashboardMonthlyTotalModel.Net),
                "Net",
                25,
                "+#,##0.00;-#,##0.00;0.00",
                DataGridViewContentAlignment.MiddleRight));

        _recentGrid = UiFactory.CreateGrid();
        _recentGrid.Name = "DashboardRecentGrid";
        _recentGrid.Columns.AddRange(
            UiFactory.CreateColumn(
                nameof(RecentTransactionRow.Date),
                "Date",
                12,
                "yyyy-MM-dd",
                DataGridViewContentAlignment.MiddleCenter),
            UiFactory.CreateColumn(nameof(RecentTransactionRow.Type), "Type", 10),
            UiFactory.CreateColumn(nameof(RecentTransactionRow.Account), "Account", 18),
            UiFactory.CreateColumn(nameof(RecentTransactionRow.Category), "Category", 16),
            UiFactory.CreateColumn(
                nameof(RecentTransactionRow.Amount),
                "Amount",
                14,
                "+#,##0.00;-#,##0.00;0.00",
                DataGridViewContentAlignment.MiddleRight),
            UiFactory.CreateColumn(nameof(RecentTransactionRow.Description), "Description", 30));
        _recentGrid.CellFormatting += OnRecentCellFormatting;

        _gridsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12, 4, 12, 12)
        };
        _gridsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        _gridsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
        _gridsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        _gridsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));

        AddGroup(_gridsPanel, "Expenses by category", _categoriesGrid, 0, 0);
        AddGroup(_gridsPanel, "Income and expenses by month", _monthlyChart, 1, 0);
        AddGroup(_gridsPanel, "Last 6 months", _trendGrid, 0, 1);
        AddGroup(_gridsPanel, "Recent transactions", _recentGrid, 1, 1);

        Controls.Add(_gridsPanel);
        Controls.Add(_cardsPanel);
        Controls.Add(_toolbar);

        SetStatus("Ready");
    }

    public override Task RefreshAsync()
    {
        return RunAsync(ReloadCoreAsync);
    }

    protected override void SetInteractionEnabled(bool enabled)
    {
        _toolbar.Enabled = enabled;
        _cardsPanel.Enabled = enabled;
        _gridsPanel.Enabled = enabled;
        UseWaitCursor = !enabled;
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
        var dashboard = await Api.GetDashboardAsync(_month);

        _month = dashboard.Month;
        _monthLabel.Text = dashboard.Month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        _totalBalanceValue.Text = FormatMoney(dashboard.Currency, dashboard.TotalBalance);
        _incomeValue.Text = FormatMoney(dashboard.Currency, dashboard.MonthIncome);
        _expensesValue.Text = FormatMoney(dashboard.Currency, dashboard.MonthExpenses);

        _monthlyChart.SetData(dashboard.MonthlyTrend, dashboard.Currency);
        _categoriesGrid.DataSource = dashboard.ExpensesByCategory.ToList();
        _trendGrid.DataSource = dashboard.MonthlyTrend.ToList();
        _recentGrid.DataSource = dashboard.RecentTransactions
            .Select(transaction => new RecentTransactionRow(transaction))
            .ToList();

        SetStatus($"Loaded dashboard for {dashboard.Month:yyyy-MM}.");
    }

    private static string FormatMoney(string currency, decimal amount)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            "{0} {1:N2}",
            currency,
            amount);
    }

    private static Label CreateCard(
        TableLayoutPanel panel,
        string title,
        string valueName,
        Color valueColor,
        int column)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(12, 8, 12, 8)
        };

        var titleLabel = new Label
        {
            Text = title,
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Dock = DockStyle.Top
        };

        var valueLabel = new Label
        {
            Name = valueName,
            Text = "-",
            ForeColor = valueColor,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Top
        };

        card.Controls.Add(valueLabel);
        card.Controls.Add(titleLabel);
        panel.Controls.Add(card, column, 0);

        return valueLabel;
    }

    private static void AddGroup(
        TableLayoutPanel panel,
        string title,
        Control content,
        int column,
        int row)
    {
        var group = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 8, 8)
        };

        content.Dock = DockStyle.Fill;
        group.Controls.Add(content);
        panel.Controls.Add(group, column, row);
    }

    private void OnRecentCellFormatting(object? sender, DataGridViewCellFormattingEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 ||
            _recentGrid.Rows[eventArgs.RowIndex].DataBoundItem is not RecentTransactionRow transaction)
        {
            return;
        }

        var style = eventArgs.CellStyle;

        if (style is null ||
            _recentGrid.Columns[eventArgs.ColumnIndex].DataPropertyName != nameof(RecentTransactionRow.Amount))
        {
            return;
        }

        style.ForeColor = transaction.Amount >= 0
            ? Color.FromArgb(21, 128, 61)
            : Color.FromArgb(185, 28, 28);
    }

    private sealed record RecentTransactionRow(TransactionModel Model)
    {
        public DateOnly Date => Model.Date;

        public TransactionType Type => Model.Type;

        public string Account => Model.AccountName;

        public string Category => Model.CategoryName ?? "Transfer";

        public decimal Amount => Model.Type is TransactionType.Income or TransactionType.TransferIn
            ? Model.Amount
            : -Model.Amount;

        public string Description => Model.Description ?? string.Empty;
    }
}
