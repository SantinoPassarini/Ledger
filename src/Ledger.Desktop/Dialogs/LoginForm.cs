using System.Drawing;
using System.Windows.Forms;
using Ledger.Desktop.Api;
using Ledger.Desktop.Ui;

namespace Ledger.Desktop.Dialogs;

public sealed class LoginForm : Form
{
    private readonly LedgerApiClient _api;
    private readonly TextBox _identifierBox;
    private readonly TextBox _passwordBox;
    private readonly Button _signInButton;
    private readonly Button _cancelButton;
    private readonly LinkLabel _createAccountLink;
    private readonly Label _statusLabel;
    private bool _busy;

    public LoginForm(LedgerApiClient api, string apiBaseUrl)
    {
        _api = api;

        Text = "Sign in to Ledger";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 300);
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(20, 18, 20, 16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            Text = "Ledger",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 2);

        var subtitle = new Label
        {
            Text = "Sign in with your Ledger account.",
            ForeColor = Color.FromArgb(71, 85, 105),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14)
        };
        layout.Controls.Add(subtitle, 0, 1);
        layout.SetColumnSpan(subtitle, 2);

        layout.Controls.Add(UiFactory.CreateLabel("Email or username"), 0, 2);
        _identifierBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 6, 0, 8),
            MaxLength = 256
        };
        layout.Controls.Add(_identifierBox, 1, 2);

        layout.Controls.Add(UiFactory.CreateLabel("Password"), 0, 3);
        _passwordBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 6, 0, 8),
            MaxLength = 200,
            UseSystemPasswordChar = true
        };
        layout.Controls.Add(_passwordBox, 1, 3);

        _statusLabel = new Label
        {
            Text = $"API: {apiBaseUrl}",
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        };
        layout.Controls.Add(_statusLabel, 0, 4);
        layout.SetColumnSpan(_statusLabel, 2);

        _createAccountLink = new LinkLabel
        {
            Text = "Create account",
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        };
        layout.Controls.Add(_createAccountLink, 0, 5);
        layout.SetColumnSpan(_createAccountLink, 2);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        };
        _signInButton = UiFactory.CreateButton("Sign in");
        _cancelButton = UiFactory.CreateButton("Cancel");
        buttons.Controls.Add(_signInButton);
        buttons.Controls.Add(_cancelButton);
        layout.Controls.Add(buttons, 0, 6);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);

        _signInButton.Click += async (_, _) => await SignInAsync();
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        _createAccountLink.LinkClicked += (_, _) => OpenRegistration();

        AcceptButton = _signInButton;
        CancelButton = _cancelButton;
        Shown += (_, _) => _identifierBox.Focus();
    }

    private void OpenRegistration()
    {
        if (_busy)
        {
            return;
        }

        using var register = new RegisterForm(_api);

        if (register.ShowDialog(this) == DialogResult.OK)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private async Task SignInAsync()
    {
        if (_busy)
        {
            return;
        }

        var identifier = _identifierBox.Text.Trim();
        var password = _passwordBox.Text;

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            _statusLabel.Text = "Enter your username or email and password.";
            _statusLabel.ForeColor = Color.FromArgb(185, 28, 28);
            return;
        }

        _busy = true;
        SetBusy(true);
        _statusLabel.ForeColor = Color.FromArgb(100, 116, 139);
        _statusLabel.Text = "Signing in...";

        try
        {
            var user = await _api.LoginAsync(identifier, password);
            _statusLabel.Text = $"Welcome, {user.DisplayName}.";
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "Sign in failed.";
            _statusLabel.ForeColor = Color.FromArgb(185, 28, 28);
            _passwordBox.Clear();
            _passwordBox.Focus();
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

    private void SetBusy(bool busy)
    {
        _identifierBox.Enabled = !busy;
        _passwordBox.Enabled = !busy;
        _signInButton.Enabled = !busy;
        _cancelButton.Enabled = !busy;
        _createAccountLink.Enabled = !busy;
    }
}
