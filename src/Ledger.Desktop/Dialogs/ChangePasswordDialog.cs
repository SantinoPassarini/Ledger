using System.Drawing;
using System.Windows.Forms;
using Ledger.Desktop.Api;
using Ledger.Desktop.Models;
using Ledger.Desktop.Ui;

namespace Ledger.Desktop.Dialogs;

public sealed class ChangePasswordDialog : Form
{
    private readonly LedgerApiClient _api;
    private readonly TextBox _currentPasswordBox;
    private readonly TextBox _newPasswordBox;
    private readonly TextBox _confirmPasswordBox;
    private readonly Button _changeButton;
    private readonly Button _cancelButton;
    private readonly Label _statusLabel;
    private bool _busy;

    public ChangePasswordDialog(LedgerApiClient api)
    {
        _api = api;

        Text = "Change password";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 300);
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
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            Text = "Change password",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 2);

        var subtitle = new Label
        {
            Text = "The new password needs at least 12 characters. " +
                   "You will be signed out after the change.",
            ForeColor = Color.FromArgb(71, 85, 105),
            AutoSize = true,
            MaximumSize = new Size(400, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.Controls.Add(subtitle, 0, 1);
        layout.SetColumnSpan(subtitle, 2);

        _currentPasswordBox = CreatePasswordBox();
        AddField(layout, 2, "Current password", _currentPasswordBox);

        _newPasswordBox = CreatePasswordBox();
        AddField(layout, 3, "New password", _newPasswordBox);

        _confirmPasswordBox = CreatePasswordBox();
        AddField(layout, 4, "Confirm new password", _confirmPasswordBox);

        _statusLabel = new Label
        {
            Text = string.Empty,
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            MaximumSize = new Size(400, 0),
            Margin = new Padding(0, 8, 0, 0)
        };
        layout.Controls.Add(_statusLabel, 0, 5);
        layout.SetColumnSpan(_statusLabel, 2);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        };
        _changeButton = UiFactory.CreateButton("Change password");
        _cancelButton = UiFactory.CreateButton("Cancel");
        buttons.Controls.Add(_changeButton);
        buttons.Controls.Add(_cancelButton);

        layout.SetColumnSpan(_statusLabel, 2);
        layout.Controls.Add(buttons, 0, 6);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);

        _changeButton.Click += async (_, _) => await ChangePasswordAsync();
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = _changeButton;
        CancelButton = _cancelButton;
        Shown += (_, _) => _currentPasswordBox.Focus();
    }

    private async Task ChangePasswordAsync()
    {
        if (_busy)
        {
            return;
        }

        var currentPassword = _currentPasswordBox.Text;
        var newPassword = _newPasswordBox.Text;
        var confirmation = _confirmPasswordBox.Text;

        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            ShowValidation("Enter your current password.");
            _currentPasswordBox.Focus();
            return;
        }

        if (newPassword.Length < 12)
        {
            ShowValidation("The new password must have at least 12 characters.");
            _newPasswordBox.Focus();
            return;
        }

        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
        {
            ShowValidation("The new passwords do not match.");
            _confirmPasswordBox.Focus();
            return;
        }

        if (string.Equals(newPassword, currentPassword, StringComparison.Ordinal))
        {
            ShowValidation("The new password must be different from the current password.");
            _newPasswordBox.Focus();
            return;
        }

        _busy = true;
        SetBusy(true);
        SetStatus("Changing your password...", Color.FromArgb(100, 116, 139));

        try
        {
            await _api.ChangePasswordAsync(
                new ChangePasswordInput(currentPassword, newPassword));

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            SetStatus("The password could not be changed.", Color.FromArgb(185, 28, 28));
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

    private static TextBox CreatePasswordBox()
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 4, 0, 8),
            MaxLength = 200,
            UseSystemPasswordChar = true
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
        _currentPasswordBox.Enabled = !busy;
        _newPasswordBox.Enabled = !busy;
        _confirmPasswordBox.Enabled = !busy;
        _changeButton.Enabled = !busy;
        _cancelButton.Enabled = !busy;
    }
}
