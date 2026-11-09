using System.Drawing;
using System.Net.Mail;
using System.Windows.Forms;
using Ledger.Desktop.Api;
using Ledger.Desktop.Models;
using Ledger.Desktop.Ui;

namespace Ledger.Desktop.Dialogs;

public sealed class RegisterForm : Form
{
    private readonly LedgerApiClient _api;
    private readonly TextBox _usernameBox;
    private readonly TextBox _emailBox;
    private readonly TextBox _displayNameBox;
    private readonly TextBox _passwordBox;
    private readonly TextBox _confirmPasswordBox;
    private readonly TextBox _inviteCodeBox;
    private readonly Button _createButton;
    private readonly Button _cancelButton;
    private readonly Label _statusLabel;
    private bool _busy;

    public RegisterForm(LedgerApiClient api)
    {
        _api = api;

        Text = "Create a Ledger account";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(500, 470);
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(20, 18, 20, 16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            Text = "Create account",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 2);

        var subtitle = new Label
        {
            Text = "Use the invite code you received. Passwords need at least 12 characters.",
            ForeColor = Color.FromArgb(71, 85, 105),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.Controls.Add(subtitle, 0, 1);
        layout.SetColumnSpan(subtitle, 2);

        _usernameBox = CreateTextBox(64);
        AddField(layout, 2, "Username", _usernameBox);

        _emailBox = CreateTextBox(256);
        AddField(layout, 3, "Email (optional)", _emailBox);

        _displayNameBox = CreateTextBox(100);
        AddField(layout, 4, "Display name (optional)", _displayNameBox);

        _passwordBox = CreateTextBox(200);
        _passwordBox.UseSystemPasswordChar = true;
        AddField(layout, 5, "Password", _passwordBox);

        _confirmPasswordBox = CreateTextBox(200);
        _confirmPasswordBox.UseSystemPasswordChar = true;
        AddField(layout, 6, "Confirm password", _confirmPasswordBox);

        _inviteCodeBox = CreateTextBox(200);
        _inviteCodeBox.UseSystemPasswordChar = true;
        AddField(layout, 7, "Invite code", _inviteCodeBox);

        _statusLabel = new Label
        {
            Text = "If you leave the email blank, Ledger uses <username>@ledger.local. " +
                   "That address cannot receive email.",
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            Margin = new Padding(0, 8, 0, 0)
        };
        layout.Controls.Add(_statusLabel, 0, 8);
        layout.SetColumnSpan(_statusLabel, 2);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        };
        _createButton = UiFactory.CreateButton("Create account");
        _cancelButton = UiFactory.CreateButton("Cancel");
        buttons.Controls.Add(_createButton);
        buttons.Controls.Add(_cancelButton);
        layout.Controls.Add(buttons, 0, 9);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);

        _createButton.Click += async (_, _) => await RegisterAsync();
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = _createButton;
        CancelButton = _cancelButton;
        Shown += (_, _) => _usernameBox.Focus();
    }

    private async Task RegisterAsync()
    {
        if (_busy)
        {
            return;
        }

        var username = _usernameBox.Text.Trim();
        var email = _emailBox.Text.Trim();
        var displayName = _displayNameBox.Text.Trim();
        var password = _passwordBox.Text;
        var confirmation = _confirmPasswordBox.Text;
        var inviteCode = _inviteCodeBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowValidation("Enter a username.");
            _usernameBox.Focus();
            return;
        }

        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
        {
            ShowValidation("Enter a valid email address or leave the field blank.");
            _emailBox.Focus();
            return;
        }

        if (password.Length < 12)
        {
            ShowValidation("The password must have at least 12 characters.");
            _passwordBox.Focus();
            return;
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            ShowValidation("The passwords do not match.");
            _confirmPasswordBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(inviteCode))
        {
            ShowValidation("Enter the invite code.");
            _inviteCodeBox.Focus();
            return;
        }

        _busy = true;
        SetBusy(true);
        SetStatus("Creating your account...", Color.FromArgb(100, 116, 139));

        try
        {
            var result = await _api.RegisterAsync(
                new RegisterInput(
                    username,
                    string.IsNullOrWhiteSpace(email) ? null : email,
                    password,
                    inviteCode,
                    string.IsNullOrWhiteSpace(displayName) ? null : displayName));

            if (result.EmailGenerated)
            {
                UiMessages.ShowInformation(
                    this,
                    $"Your account was created. Ledger uses {result.User.Email} as your " +
                    "identifier because you did not provide an email address. " +
                    "That address cannot receive email.");
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            SetStatus("Account creation failed.", Color.FromArgb(185, 28, 28));
            UiMessages.ShowError(this, exception);
        }
        finally
        {
            _busy = false;

            if (!IsDisposed && !Disposing)
            {
                SetBusy(false);
            }
        }
    }

    private static TextBox CreateTextBox(int maxLength)
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 4, 0, 8),
            MaxLength = maxLength
        };
    }

    private static void AddField(
        TableLayoutPanel layout,
        int row,
        string label,
        Control control)
    {
        layout.Controls.Add(UiFactory.CreateLabel(label), 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void ShowValidation(string message)
    {
        SetStatus(message, Color.FromArgb(185, 28, 28));
    }

    private void SetStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
    }

    private void SetBusy(bool busy)
    {
        _usernameBox.Enabled = !busy;
        _emailBox.Enabled = !busy;
        _displayNameBox.Enabled = !busy;
        _passwordBox.Enabled = !busy;
        _confirmPasswordBox.Enabled = !busy;
        _inviteCodeBox.Enabled = !busy;
        _createButton.Enabled = !busy;
        _cancelButton.Enabled = !busy;
    }
}
