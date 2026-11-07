using System.Drawing;
using System.Windows.Forms;
using Ledger.Desktop.Models;

namespace Ledger.Desktop.Dialogs;

public sealed class TransferDialog : Form
{
    private readonly ComboBox _fromCombo;
    private readonly ComboBox _toCombo;
    private readonly NumericUpDown _amountInput;
    private readonly DateTimePicker _datePicker;
    private readonly TextBox _descriptionBox;
    private readonly Button _okButton;
    private readonly Button _cancelButton;

    public TransferDialog(
        IReadOnlyList<AccountModel> accounts,
        TransferInput? initial = null)
    {
        Text = "New transfer";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 330);
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(16, 16, 16, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var i = 0; i < 5; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _fromCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10)
        };
        _fromCombo.DataSource = accounts.ToList();
        _fromCombo.DisplayMember = nameof(AccountModel.Name);

        _toCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10)
        };
        _toCombo.DataSource = accounts.ToList();
        _toCombo.DisplayMember = nameof(AccountModel.Name);

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

        layout.Controls.Add(CreateLabel("From account"), 0, 0);
        layout.Controls.Add(_fromCombo, 1, 0);
        layout.Controls.Add(CreateLabel("To account"), 0, 1);
        layout.Controls.Add(_toCombo, 1, 1);
        layout.Controls.Add(CreateLabel("Amount"), 0, 2);
        layout.Controls.Add(_amountInput, 1, 2);
        layout.Controls.Add(CreateLabel("Date"), 0, 3);
        layout.Controls.Add(_datePicker, 1, 3);
        layout.Controls.Add(CreateLabel("Description"), 0, 4);
        layout.Controls.Add(_descriptionBox, 1, 4);
        layout.Controls.Add(buttonPanel, 0, 5);
        layout.SetColumnSpan(buttonPanel, 2);

        Controls.Add(layout);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        if (initial is not null)
        {
            SelectAccount(_fromCombo, initial.FromAccountId);
            SelectAccount(_toCombo, initial.ToAccountId);
            _amountInput.Value = Math.Clamp(initial.Amount, _amountInput.Minimum, _amountInput.Maximum);
            _datePicker.Value = ClampDate(initial.Date.ToDateTime(TimeOnly.MinValue));
            _descriptionBox.Text = initial.Description ?? string.Empty;
        }
        else
        {
            _amountInput.Value = 0.01m;
            _datePicker.Value = DateTime.Today;

            if (_fromCombo.Items.Count > 0)
            {
                _fromCombo.SelectedIndex = 0;
            }

            if (_toCombo.Items.Count > 1)
            {
                _toCombo.SelectedIndex = 1;
            }
            else if (_toCombo.Items.Count > 0)
            {
                _toCombo.SelectedIndex = 0;
            }
        }
    }

    public TransferInput Input { get; private set; } = default!;

    private static void SelectAccount(ComboBox comboBox, Guid accountId)
    {
        for (var i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is AccountModel account && account.Id == accountId)
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
        if (_fromCombo.SelectedItem is not AccountModel fromAccount)
        {
            ShowValidation("Select a source account.");
            return;
        }

        if (_toCombo.SelectedItem is not AccountModel toAccount)
        {
            ShowValidation("Select a destination account.");
            return;
        }

        if (fromAccount.Id == toAccount.Id)
        {
            ShowValidation("Source and destination accounts must be different.");
            return;
        }

        Input = new TransferInput(
            fromAccount.Id,
            toAccount.Id,
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
