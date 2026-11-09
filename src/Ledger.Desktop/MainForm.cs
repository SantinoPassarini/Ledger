using System.Drawing;
using System.Windows.Forms;
using Ledger.Desktop.Api;
using Ledger.Desktop.Dialogs;
using Ledger.Desktop.Views;

namespace Ledger.Desktop;

public sealed class MainForm : Form
{
    private readonly LedgerApiClient _api;
    private readonly TabControl _tabs;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly Label _userLabel;
    private readonly Button _changePasswordButton;
    private readonly Button _signOutButton;
    private readonly DashboardView _dashboardView;
    private readonly AccountsView _accountsView;
    private readonly CategoriesView _categoriesView;
    private readonly BudgetsView _budgetsView;
    private readonly TransactionsView _transactionsView;
    private bool _initialized;

    public MainForm(LedgerApiClient api, string apiBaseUrl)
    {
        _api = api;

        Text = "Ledger";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1180, 720);
        MinimumSize = new Size(980, 600);
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;

        _statusStrip = new StatusStrip
        {
            SizingGrip = false,
            BackColor = SystemColors.Control
        };
        _statusLabel = new ToolStripStatusLabel("Ready")
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var apiLabel = new ToolStripStatusLabel($"API: {apiBaseUrl}");
        _statusStrip.Items.AddRange([_statusLabel, apiLabel]);

        _dashboardView = new DashboardView(api, SetStatus);
        _accountsView = new AccountsView(api, SetStatus);
        _categoriesView = new CategoriesView(api, SetStatus);
        _budgetsView = new BudgetsView(api, SetStatus);
        _transactionsView = new TransactionsView(api, SetStatus);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            Padding = new Point(16, 6)
        };
        _tabs.TabPages.Add(CreatePage("Dashboard", _dashboardView));
        _tabs.TabPages.Add(CreatePage("Accounts", _accountsView));
        _tabs.TabPages.Add(CreatePage("Categories", _categoriesView));
        _tabs.TabPages.Add(CreatePage("Budgets", _budgetsView));
        _tabs.TabPages.Add(CreatePage("Transactions", _transactionsView));

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = Color.FromArgb(30, 41, 59)
        };
        header.Controls.Add(new Label
        {
            Text = "Ledger",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 8)
        });
        header.Controls.Add(new Label
        {
            Text = "Personal finance",
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Location = new Point(18, 38)
        });

        var accountPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0, 18, 16, 0)
        };
        _signOutButton = new Button
        {
            Text = "Sign out",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(84, 28),
            Padding = new Padding(10, 2, 10, 2),
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = true
        };
        _changePasswordButton = new Button
        {
            Text = "Change password",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(120, 28),
            Padding = new Padding(10, 2, 10, 2),
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = true
        };
        _userLabel = new Label
        {
            Text = api.CurrentUser?.Username ?? api.CurrentUser?.Email ?? string.Empty,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };
        accountPanel.Controls.Add(_signOutButton);
        accountPanel.Controls.Add(_changePasswordButton);
        accountPanel.Controls.Add(_userLabel);
        header.Controls.Add(accountPanel);

        _signOutButton.Click += (_, _) => SignOut();
        _changePasswordButton.Click += (_, _) => ChangePassword();

        _api.SessionExpired += OnSessionExpired;
        FormClosed += (_, _) => _api.SessionExpired -= OnSessionExpired;

        Controls.Add(_tabs);
        Controls.Add(_statusStrip);
        Controls.Add(header);

        _tabs.SelectedIndexChanged += async (_, _) =>
        {
            if (_initialized)
            {
                await RefreshActiveTabAsync();
            }
        };

        Shown += async (_, _) =>
        {
            _initialized = true;
            await RefreshActiveTabAsync();
        };
    }

    private static TabPage CreatePage(string text, Control content)
    {
        var page = new TabPage(text)
        {
            Padding = new Padding(0)
        };

        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }

    public bool SignOutRequested { get; private set; }

    public bool SessionExpired { get; private set; }

    private async Task RefreshActiveTabAsync()
    {
        if (_tabs.SelectedTab?.Controls.Count > 0 &&
            _tabs.SelectedTab.Controls[0] is ApiViewBase view)
        {
            await view.RefreshAsync();
        }
    }

    private void SignOut()
    {
        if (SignOutRequested)
        {
            return;
        }

        SignOutRequested = true;
        _api.Logout();
        Close();
    }

    private void ChangePassword()
    {
        using var dialog = new ChangePasswordDialog(_api);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        MessageBox.Show(
            this,
            "Your password was changed. Sign in again with your new password.",
            "Ledger",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        SignOut();
    }

    private void OnSessionExpired(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing || SignOutRequested)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnSessionExpired(sender, e)));
            return;
        }

        SessionExpired = true;
        SignOutRequested = true;
        _api.Logout();
        Close();
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }
}
