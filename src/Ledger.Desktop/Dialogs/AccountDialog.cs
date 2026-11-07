using System.Drawing;
using System.Windows.Forms;
using Ledger.Desktop.Models;

namespace Ledger.Desktop.Dialogs;

public sealed class AccountDialog : Form
{
    private readonly TextBox _nameBox;
    private readonly ComboBox _typeCombo;
    private readonly Button _okButton;
    private readonly Button _cancelButton;

    public AccountDialog(string title, string name, AccountType type)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 170);
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(16, 16, 16, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _nameBox = new TextBox
        {
            MaxLength = 100,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10)
        };

        _typeCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10)
        };
        _typeCombo.DataSource = Enum.GetValues<AccountType>();
        _typeCombo.SelectedItem = type;

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

        layout.Controls.Add(CreateLabel("Name"), 0, 0);
        layout.Controls.Add(_nameBox, 1, 0);
        layout.Controls.Add(CreateLabel("Type"), 0, 1);
        layout.Controls.Add(_typeCombo, 1, 1);
        layout.Controls.Add(buttonPanel, 0, 2);
        layout.SetColumnSpan(buttonPanel, 2);

        Controls.Add(layout);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        _nameBox.Text = name;
    }

    public string AccountName => _nameBox.Text.Trim();

    public AccountType SelectedType => _typeCombo.SelectedItem is AccountType type
        ? type
        : AccountType.Cash;

    private void OnOkClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AccountName))
        {
            MessageBox.Show(
                this,
                "Name is required.",
                "Ledger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

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
