using System.Drawing;
using System.Windows.Forms;
using Ledger.Desktop.Api;
using Ledger.Desktop.Views;

namespace Ledger.Desktop;

public sealed class MainForm : Form
{
    private readonly TabControl _tabs;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly AccountsView _accountsView;
    private readonly CategoriesView _categoriesView;
    private readonly TransactionsView _transactionsView;
    private bool _initialized;

    public MainForm(LedgerApiClient api, string apiBaseUrl)
    {
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

        _accountsView = new AccountsView(api, SetStatus);
        _categoriesView = new CategoriesView(api, SetStatus);
        _transactionsView = new TransactionsView(api, SetStatus);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            Padding = new Point(16, 6)
        };
        _tabs.TabPages.Add(CreatePage("Accounts", _accountsView));
        _tabs.TabPages.Add(CreatePage("Categories", _categoriesView));
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

    private async Task RefreshActiveTabAsync()
    {
        if (_tabs.SelectedTab?.Controls.Count > 0 &&
            _tabs.SelectedTab.Controls[0] is ApiViewBase view)
        {
            await view.RefreshAsync();
        }
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }
}
