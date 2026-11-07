using System.Drawing;
using System.Windows.Forms;
using Ledger.Desktop.Models;

namespace Ledger.Desktop.Dialogs;

public sealed class TransactionDialog : Form
{
    private readonly ComboBox _typeCombo;
    private readonly ComboBox _accountCombo;
    private readonly ComboBox _categoryCombo;
    private readonly NumericUpDown _amountInput;
    private readonly DateTimePicker _datePicker;
    private readonly TextBox _descriptionBox;
    private readonly Button _okButton;
    private readonly Button _cancelButton;
    private readonly IReadOnlyList<CategoryModel> _categories;
    private bool _updatingCategories;

    public TransactionDialog(
        string title,
        IReadOnlyList<AccountModel> accounts,
        IReadOnlyList<CategoryModel> categories,
        TransactionInput? initial)
    {
        _categories = categories;

        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 360);
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(16, 16, 16, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var i = 0; i < 6; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _typeCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10)
        };
        _typeCombo.DataSource = new[] { TransactionType.Income, TransactionType.Expense };
        _typeCombo.SelectedIndexChanged += (_, _) =>
        {
            if (!_updatingCategories)
            {
                PopulateCategories();
            }
        };

        _accountCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10)
        };
        _accountCombo.DataSource = accounts.ToList();
        _accountCombo.DisplayMember = nameof(AccountModel.Name);

        _categoryCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10)
        };

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

        _datePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10)
        };

        _descriptionBox = new TextBox
        {
            MaxLength = 200,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10)
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

        layout.Controls.Add(CreateLabel("Type"), 0, 0);
        layout.Controls.Add(_typeCombo, 1, 0);
        layout.Controls.Add(CreateLabel("Account"), 0, 1);
        layout.Controls.Add(_accountCombo, 1, 1);
        layout.Controls.Add(CreateLabel("Category"), 0, 2);
        layout.Controls.Add(_categoryCombo, 1, 2);
        layout.Controls.Add(CreateLabel("Amount"), 0, 3);
        layout.Controls.Add(_amountInput, 1, 3);
        layout.Controls.Add(CreateLabel("Date"), 0, 4);
        layout.Controls.Add(_datePicker, 1, 4);
        layout.Controls.Add(CreateLabel("Description"), 0, 5);
        layout.Controls.Add(_descriptionBox, 1, 5);
        layout.Controls.Add(buttonPanel, 0, 6);
        layout.SetColumnSpan(buttonPanel, 2);

        Controls.Add(layout);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        if (initial is not null)
        {
            _typeCombo.SelectedItem = initial.Type is TransactionType.TransferIn
                ? TransactionType.Income
                : initial.Type is TransactionType.TransferOut
                    ? TransactionType.Expense
                    : initial.Type;

            PopulateCategories();
            SelectAccount(initial.AccountId);

            if (initial.CategoryId is Guid categoryId)
            {
                SelectCategory(categoryId);
            }
            _amountInput.Value = Math.Clamp(initial.Amount, _amountInput.Minimum, _amountInput.Maximum);
            _datePicker.Value = ClampDate(initial.Date.ToDateTime(TimeOnly.MinValue));
            _descriptionBox.Text = initial.Description ?? string.Empty;
        }
        else
        {
            _typeCombo.SelectedItem = TransactionType.Income;
            PopulateCategories();

            if (_accountCombo.Items.Count > 0)
            {
                _accountCombo.SelectedIndex = 0;
            }

            _amountInput.Value = 0.01m;
            _datePicker.Value = DateTime.Today;
        }
    }

    public TransactionInput Input { get; private set; } = default!;

    private TransactionType SelectedType => _typeCombo.SelectedItem is TransactionType type
        ? type
        : TransactionType.Income;

    private void PopulateCategories()
    {
        _updatingCategories = true;

        try
        {
            var selectedId = (_categoryCombo.SelectedItem as CategoryModel)?.Id;
            var kind = SelectedType == TransactionType.Income
                ? CategoryKind.Income
                : CategoryKind.Expense;
            var items = _categories.Where(category => category.Kind == kind).ToList();

            _categoryCombo.DataSource = null;
            _categoryCombo.DataSource = items;
            _categoryCombo.DisplayMember = nameof(CategoryModel.Name);

            if (selectedId is Guid id)
            {
                var index = items.FindIndex(category => category.Id == id);

                if (index >= 0)
                {
                    _categoryCombo.SelectedIndex = index;
                }
            }

            if (_categoryCombo.SelectedIndex < 0 && items.Count > 0)
            {
                _categoryCombo.SelectedIndex = 0;
            }
        }
        finally
        {
            _updatingCategories = false;
        }
    }

    private void SelectAccount(Guid accountId)
    {
        for (var i = 0; i < _accountCombo.Items.Count; i++)
        {
            if (_accountCombo.Items[i] is AccountModel account && account.Id == accountId)
            {
                _accountCombo.SelectedIndex = i;
                return;
            }
        }

        if (_accountCombo.Items.Count > 0)
        {
            _accountCombo.SelectedIndex = 0;
        }
    }

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

    private DateTime ClampDate(DateTime value)
    {
        if (value < _datePicker.MinDate)
        {
            return _datePicker.MinDate;
        }

        return value > _datePicker.MaxDate ? _datePicker.MaxDate : value;
    }

    private void OnOkClicked(object? sender, EventArgs e)
    {
        if (_accountCombo.SelectedItem is not AccountModel account)
        {
            ShowValidation("Select an account.");
            return;
        }

        if (_categoryCombo.SelectedItem is not CategoryModel category)
        {
            ShowValidation("Select a category.");
            return;
        }

        var type = SelectedType;

        if (type is not TransactionType.Income and not TransactionType.Expense)
        {
            ShowValidation("Only income and expense transactions are supported.");
            return;
        }

        Input = new TransactionInput(
            account.Id,
            category.Id,
            type,
            _amountInput.Value,
            DateOnly.FromDateTime(_datePicker.Value),
            string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text.Trim());

        DialogResult = DialogResult.OK;
    }

    private void ShowValidation(string message)
    {
        MessageBox.Show(
            this,
            message,
            "Ledger",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
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
