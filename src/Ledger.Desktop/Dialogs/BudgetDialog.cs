using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Ledger.Desktop.Models;

namespace Ledger.Desktop.Dialogs;

public sealed class BudgetDialog : Form
{
    private readonly ComboBox _categoryCombo;
    private readonly NumericUpDown _amountInput;
    private readonly Button _okButton;
    private readonly Button _cancelButton;
    private readonly DateOnly _month;

    public BudgetDialog(
        string title,
        IReadOnlyList<CategoryModel> categories,
        DateOnly month,
        BudgetModel? initial = null)
    {
        _month = new DateOnly(month.Year, month.Month, 1);

        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(400, 200);
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(16, 16, 16, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _categoryCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10)
        };
        _categoryCombo.DataSource = categories.ToList();
        _categoryCombo.DisplayMember = nameof(CategoryModel.Name);

        _amountInput = new NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0.01m,
            Maximum = 999_999_999_999_999.99m,
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Right,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10)
        };

        var monthLabel = new Label
        {
            Text = _month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 0, 8)
        };

        _okButton = new Button
        {
            Text = "OK",
            AutoSize = true,
            MinimumSize = new Size(84, 30),
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = true
        };
        _okButton.Click += OnOkClicked;

        _cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            MinimumSize = new Size(84, 30),
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(0, 0, 0, 0),
            UseVisualStyleBackColor = true
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0)
        };
        buttonPanel.Controls.Add(_cancelButton);
        buttonPanel.Controls.Add(_okButton);

        layout.Controls.Add(CreateLabel("Category"), 0, 0);
        layout.Controls.Add(_categoryCombo, 1, 0);
        layout.Controls.Add(CreateLabel("Month"), 0, 1);
        layout.Controls.Add(monthLabel, 1, 1);
        layout.Controls.Add(CreateLabel("Amount"), 0, 2);
        layout.Controls.Add(_amountInput, 1, 2);
        layout.Controls.Add(buttonPanel, 0, 3);
        layout.SetColumnSpan(buttonPanel, 2);

        Controls.Add(layout);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        if (initial is not null)
        {
            SelectCategory(initial.CategoryId);
            _amountInput.Value = Math.Clamp(
                initial.Amount,
                _amountInput.Minimum,
                _amountInput.Maximum);
            _categoryCombo.Enabled = false;
        }
        else
        {
            _amountInput.Value = 0.01m;

            if (_categoryCombo.Items.Count > 0)
            {
                _categoryCombo.SelectedIndex = 0;
            }
        }
    }

    public BudgetInput Input { get; private set; } = default!;

    private void SelectCategory(Guid categoryId)
    {
        for (var i = 0; i < _categoryCombo.Items.Count; i++)
        {
            if (_categoryCombo.Items[i] is CategoryModel category && category.Id == categoryId)
            {
                _categoryCombo.SelectedIndex = i;
                return;
            }
        }

        if (_categoryCombo.Items.Count > 0)
        {
            _categoryCombo.SelectedIndex = 0;
        }
    }

    private void OnOkClicked(object? sender, EventArgs e)
    {
        if (_categoryCombo.SelectedItem is not CategoryModel category)
        {
            MessageBox.Show(
                this,
                "Select an expense category.",
                "Ledger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Input = new BudgetInput(category.Id, _month, _amountInput.Value);
        DialogResult = DialogResult.OK;
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 8)
        };
    }
}
