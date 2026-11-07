using System.Drawing;
using System.Windows.Forms;
using Ledger.Desktop.Api;
using Ledger.Desktop.Dialogs;
using Ledger.Desktop.Models;
using Ledger.Desktop.Ui;

namespace Ledger.Desktop.Views;

public sealed class CategoriesView : ApiViewBase
{
    private readonly DataGridView _grid;
    private readonly FlowLayoutPanel _toolbar;
    private readonly ComboBox _kindFilter;
    private readonly CheckBox _showArchived;
    private readonly Button _newButton;
    private readonly Button _editButton;
    private readonly Button _archiveButton;
    private readonly Button _refreshButton;

    public CategoriesView(LedgerApiClient api, Action<string> setStatus)
        : base(api, setStatus)
    {
        Dock = DockStyle.Fill;

        _grid = UiFactory.CreateGrid();
        _grid.Columns.AddRange(
            UiFactory.CreateColumn(nameof(CategoryModel.Name), "Name", 55),
            UiFactory.CreateColumn(nameof(CategoryModel.Kind), "Kind", 22),
            UiFactory.CreateColumn(
                nameof(CategoryModel.IsArchived),
                "Archived",
                13,
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

        _kindFilter = UiFactory.CreateComboBox(130);
        _kindFilter.DataSource = new[]
        {
            new KindFilterOption("All kinds", null),
            new KindFilterOption("Income", CategoryKind.Income),
            new KindFilterOption("Expense", CategoryKind.Expense)
        };
        _kindFilter.DisplayMember = nameof(KindFilterOption.Label);
        _kindFilter.SelectedIndexChanged += async (_, _) => await RunAsync(ReloadCoreAsync);

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
            UiFactory.CreateLabel("Kind"),
            _kindFilter,
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
        var selected = SelectedCategory();

        _newButton.Enabled = !IsBusy;
        _refreshButton.Enabled = !IsBusy;
        _editButton.Enabled = !IsBusy && selected is { IsArchived: false };
        _archiveButton.Enabled = !IsBusy && selected is { IsArchived: false };
    }

    private CategoryModel? SelectedCategory()
    {
        return _grid.CurrentRow?.DataBoundItem as CategoryModel;
    }

    private async Task ReloadCoreAsync()
    {
        var selectedId = SelectedCategory()?.Id;
        var filter = _kindFilter.SelectedItem as KindFilterOption;
        var categories = await Api.GetCategoriesAsync(filter?.Kind, _showArchived.Checked);

        _grid.DataSource = categories.ToList();

        if (selectedId is Guid id)
        {
            SelectRow(id);
        }

        SetStatus($"Loaded {categories.Count} categor{(categories.Count == 1 ? "y" : "ies")}.");
    }

    private void SelectRow(Guid id)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.DataBoundItem is CategoryModel category && category.Id == id)
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
        var filter = _kindFilter.SelectedItem as KindFilterOption;
        var kind = filter?.Kind ?? CategoryKind.Expense;

        while (true)
        {
            using var dialog = new CategoryDialog("New category", name, kind);

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await Api.CreateCategoryAsync(new CategoryInput(dialog.CategoryName, dialog.SelectedKind));
                SetStatus("Category created.");
                await ReloadCoreAsync();
                return;
            }
            catch (Exception exception) when (
                exception is ApiException or HttpRequestException or TaskCanceledException)
            {
                UiMessages.ShowError(this, exception);
                name = dialog.CategoryName;
                kind = dialog.SelectedKind;
            }
        }
    }

    private async Task EditSelectedCoreAsync()
    {
        var category = SelectedCategory();

        if (category is null || category.IsArchived)
        {
            return;
        }

        var name = category.Name;
        var kind = category.Kind;

        while (true)
        {
            using var dialog = new CategoryDialog("Edit category", name, kind);

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await Api.UpdateCategoryAsync(
                    category.Id,
                    new CategoryInput(dialog.CategoryName, dialog.SelectedKind));
                SetStatus("Category updated.");
                await ReloadCoreAsync();
                return;
            }
            catch (Exception exception) when (
                exception is ApiException or HttpRequestException or TaskCanceledException)
            {
                UiMessages.ShowError(this, exception);
                name = dialog.CategoryName;
                kind = dialog.SelectedKind;
            }
        }
    }

    private async Task ArchiveSelectedCoreAsync()
    {
        var category = SelectedCategory();

        if (category is null || category.IsArchived)
        {
            return;
        }

        if (!UiMessages.ConfirmArchive(this, $"category \"{category.Name}\""))
        {
            return;
        }

        await Api.ArchiveCategoryAsync(category.Id);
        SetStatus("Category archived.");
        await ReloadCoreAsync();
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 ||
            _grid.Rows[eventArgs.RowIndex].DataBoundItem is not CategoryModel category)
        {
            return;
        }

        var style = eventArgs.CellStyle;

        if (style is null)
        {
            return;
        }

        style.ForeColor = category.IsArchived
            ? SystemColors.GrayText
            : _grid.DefaultCellStyle.ForeColor;
    }

    private sealed record KindFilterOption(string Label, CategoryKind? Kind);
}
