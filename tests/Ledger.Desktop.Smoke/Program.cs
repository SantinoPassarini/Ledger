using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using Ledger.Desktop;
using Ledger.Desktop.Api;
using Ledger.Desktop.Dialogs;
using Ledger.Desktop.Models;

internal static class Program
{
    private static int _failures;
    private static string _screenshotDirectory = string.Empty;
    private static bool _captureScreenshots;

    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var apiBaseUrl = GetOption(args, "--api")
            ?? Environment.GetEnvironmentVariable("LEDGER_API_URL")
            ?? "http://localhost:5100/";
        _captureScreenshots = args.Contains("--screenshots", StringComparer.OrdinalIgnoreCase);
        _screenshotDirectory = GetOption(args, "--screenshot-dir")
            ?? Path.Combine(FindRepositoryRoot(), "artifacts", "smoke");

        using var api = new LedgerApiClient(apiBaseUrl);
        var suffix = Guid.NewGuid().ToString("N");
        var accountName = $"[TEST] Desktop {suffix}";
        var transferAccountName = $"[TEST] Desktop transfer {suffix}";
        var categoryName = $"[TEST] Desktop category {suffix}";
        var description = $"[TEST] desktop smoke {suffix}";
        var transferDescription = $"[TEST] desktop transfer {suffix}";
        AccountModel? account = null;
        AccountModel? transferAccount = null;
        CategoryModel? category = null;
        TransactionModel? transaction = null;
        TransferModel? transfer = null;

        try
        {
            var accounts = await api.GetAccountsAsync(includeArchived: true);
            Check("GET accounts", true, $"{accounts.Count} account(s)");

            var categories = await api.GetCategoriesAsync(includeArchived: true);
            Check("GET categories", categories.Count >= 15, $"{categories.Count} category(ies)");

            var firstPage = await api.GetTransactionsAsync(new TransactionQuery { PageSize = 5 });
            Check("GET transactions", firstPage.PageSize == 5, $"total={firstPage.TotalCount}");

            account = await api.CreateAccountAsync(new AccountInput(accountName, AccountType.Wallet));
            Check("create account", account.Balance == 0m, account.Id.ToString());

            transferAccount = await api.CreateAccountAsync(
                new AccountInput(transferAccountName, AccountType.Wallet));
            Check("create transfer account", transferAccount.Balance == 0m, transferAccount.Id.ToString());

            category = await api.CreateCategoryAsync(
                new CategoryInput(categoryName, CategoryKind.Income));
            Check("create category", category.Kind == CategoryKind.Income);

            transaction = await api.CreateTransactionAsync(
                new TransactionInput(
                    account.Id,
                    category.Id,
                    TransactionType.Income,
                    123.45m,
                    DateOnly.FromDateTime(DateTime.Today),
                    description));
            Check("create transaction", transaction.Amount == 123.45m, transaction.AccountName);

            var afterCreate = (await api.GetAccountsAsync(includeArchived: true))
                .Single(candidate => candidate.Id == account.Id);
            Check("balance after income", afterCreate.Balance == 123.45m, afterCreate.Balance.ToString("N2"));

            await api.UpdateTransactionAsync(
                transaction.Id,
                new TransactionInput(
                    account.Id,
                    category.Id,
                    TransactionType.Income,
                    200.50m,
                    DateOnly.FromDateTime(DateTime.Today),
                    description));

            var afterUpdate = (await api.GetAccountsAsync(includeArchived: true))
                .Single(candidate => candidate.Id == account.Id);
            Check("balance after update", afterUpdate.Balance == 200.50m, afterUpdate.Balance.ToString("N2"));

            var search = await api.GetTransactionsAsync(
                new TransactionQuery
                {
                    Search = description,
                    IncludeArchived = true
                });
            Check("search updated transaction", search.TotalCount == 1, search.TotalCount.ToString());

            transfer = await api.CreateTransferAsync(
                new TransferInput(
                    account.Id,
                    transferAccount.Id,
                    50.25m,
                    DateOnly.FromDateTime(DateTime.Today),
                    transferDescription));
            Check(
                "create transfer",
                transfer.OutTransaction.TransferGroupId == transfer.TransferGroupId &&
                transfer.InTransaction.TransferGroupId == transfer.TransferGroupId &&
                transfer.OutTransaction.Type == TransactionType.TransferOut &&
                transfer.InTransaction.Type == TransactionType.TransferIn &&
                transfer.OutTransaction.CategoryId is null &&
                transfer.InTransaction.CategoryId is null,
                transfer.TransferGroupId.ToString());

            var afterTransfer = await api.GetAccountsAsync(includeArchived: true);
            var sourceAfterTransfer = afterTransfer.Single(candidate => candidate.Id == account.Id);
            var destinationAfterTransfer = afterTransfer.Single(candidate => candidate.Id == transferAccount.Id);
            Check(
                "balances after transfer",
                sourceAfterTransfer.Balance == 150.25m && destinationAfterTransfer.Balance == 50.25m,
                $"{sourceAfterTransfer.Balance:N2} -> {destinationAfterTransfer.Balance:N2}");

            var transferSearch = await api.GetTransactionsAsync(
                new TransactionQuery
                {
                    Search = transferDescription,
                    IncludeArchived = true
                });
            Check("search transfer legs", transferSearch.TotalCount == 2, transferSearch.TotalCount.ToString());

            RunUiSmoke(
                api,
                apiBaseUrl,
                account,
                transferAccount,
                category,
                description,
                transferDescription);

            await api.ArchiveTransferAsync(transfer.TransferGroupId);

            var afterTransferArchive = await api.GetAccountsAsync(includeArchived: true);
            Check(
                "balances after transfer archive",
                afterTransferArchive.Single(candidate => candidate.Id == account.Id).Balance == 200.50m &&
                afterTransferArchive.Single(candidate => candidate.Id == transferAccount.Id).Balance == 0m,
                "transfer legs archived");

            await api.ArchiveTransactionAsync(transaction.Id);

            var afterArchive = (await api.GetAccountsAsync(includeArchived: true))
                .Single(candidate => candidate.Id == account.Id);
            Check("balance after archive", afterArchive.Balance == 0m, afterArchive.Balance.ToString("N2"));

            var archivedTransactions = await api.GetTransactionsAsync(
                new TransactionQuery
                {
                    Search = description,
                    IncludeArchived = true
                });
            Check(
                "archived transaction is visible with includeArchived",
                archivedTransactions.TotalCount == 1 && archivedTransactions.Items[0].IsArchived);

            await api.ArchiveAccountAsync(account.Id);
            await api.ArchiveAccountAsync(transferAccount.Id);
            await api.ArchiveCategoryAsync(category.Id);

            var hiddenAccounts = await api.GetAccountsAsync(includeArchived: false);
            Check("archived account hidden by default", hiddenAccounts.All(candidate => candidate.Id != account.Id));

            var visibleAccounts = await api.GetAccountsAsync(includeArchived: true);
            Check(
                "archived account visible with includeArchived",
                visibleAccounts.Any(candidate => candidate.Id == account.Id && candidate.IsArchived));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"FAIL | unexpected exception | {exception}");
            _failures++;
        }
        finally
        {
            if (transfer is not null)
            {
                try
                {
                    await api.ArchiveTransferAsync(transfer.TransferGroupId);
                }
                catch
                {
                }
            }

            if (transaction is not null && account is not null)
            {
                try
                {
                    await api.ArchiveTransactionAsync(transaction.Id);
                }
                catch
                {
                }
            }

            if (account is not null)
            {
                try
                {
                    await api.ArchiveAccountAsync(account.Id);
                }
                catch
                {
                }
            }

            if (transferAccount is not null)
            {
                try
                {
                    await api.ArchiveAccountAsync(transferAccount.Id);
                }
                catch
                {
                }
            }

            if (category is not null)
            {
                try
                {
                    await api.ArchiveCategoryAsync(category.Id);
                }
                catch
                {
                }
            }
        }

        Console.WriteLine($"FAILURES={_failures}");
        return _failures == 0 ? 0 : 1;
    }

    private static void RunUiSmoke(
        LedgerApiClient api,
        string apiBaseUrl,
        AccountModel account,
        AccountModel transferAccount,
        CategoryModel category,
        string description,
        string transferDescription)
    {
        Exception? uiException = null;

        var thread = new Thread(() =>
        {
            try
            {
                using (var accountDialog = new AccountDialog("New account", string.Empty, AccountType.Cash))
                {
                    accountDialog.CreateControl();
                    Check("AccountDialog constructs", true);
                }

                using (var categoryDialog = new CategoryDialog("New category", string.Empty, CategoryKind.Expense))
                {
                    categoryDialog.CreateControl();
                    Check("CategoryDialog constructs", true);
                }

                using (var transactionDialog = new TransactionDialog(
                           "New transaction",
                           new[] { account },
                           new[] { category },
                           new TransactionInput(
                               account.Id,
                               category.Id,
                               TransactionType.Income,
                               123.45m,
                               DateOnly.FromDateTime(DateTime.Today),
                               description)))
                {
                    transactionDialog.CreateControl();
                    Check("TransactionDialog constructs", true);
                }

                using (var transferDialog = new TransferDialog(
                           new[] { account, transferAccount },
                           new TransferInput(
                               account.Id,
                               transferAccount.Id,
                               50.25m,
                               DateOnly.FromDateTime(DateTime.Today),
                               transferDescription)))
                {
                    transferDialog.CreateControl();
                    Check("TransferDialog constructs", true);
                }

                using var form = new MainForm(api, apiBaseUrl);
                form.CreateControl();
                Check("MainForm constructs", true);

                var tabs = (TabControl?)typeof(MainForm)
                    .GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance)?
                    .GetValue(form);
                var statusStrip = (StatusStrip?)typeof(MainForm)
                    .GetField("_statusStrip", BindingFlags.NonPublic | BindingFlags.Instance)?
                    .GetValue(form);

                var step = 0;
                using var timer = new System.Windows.Forms.Timer { Interval = 4000 };
                timer.Tick += (_, _) =>
                {
                    try
                    {
                        switch (step)
                        {
                            case 0:
                                Capture(form, "ledger-ui-accounts.png");
                                Check("Accounts tab renders", true);
                                Check("MainForm tabs found", tabs is not null);
                                Check("MainForm status strip found", statusStrip is not null);

                                if (tabs is not null && statusStrip is not null)
                                {
                                    Check("Layout: tabs below header", tabs.Top >= 64, $"top={tabs.Top}");
                                    Check(
                                        "Layout: tabs above status strip",
                                        tabs.Bottom <= statusStrip.Top,
                                        $"tabsBottom={tabs.Bottom} statusTop={statusStrip.Top}");

                                    var accountsGrid = FindGrid(tabs.TabPages[0]);
                                    Check(
                                        "Accounts grid contains test account",
                                        accountsGrid is not null && GridContains(accountsGrid, account.Name),
                                        $"rows={accountsGrid?.Rows.Count}");
                                    Check(
                                        "Accounts grid columns",
                                        accountsGrid?.Columns.Count == 5,
                                        $"columns={accountsGrid?.Columns.Count}");

                                    tabs.SelectedIndex = 2;
                                }

                                step = 1;
                                break;

                            case 1:
                                Capture(form, "ledger-ui-transactions.png");
                                Check("Transactions tab renders", true);

                                if (tabs is not null && statusStrip is not null)
                                {
                                    var transactionsGrid = FindGrid(tabs.TabPages[2]);
                                    var txDataSourceCount =
                                        transactionsGrid?.DataSource is System.Collections.ICollection collection
                                            ? collection.Count
                                            : -1;
                                    var statusText = statusStrip.Items.Count > 0
                                        ? statusStrip.Items[0].Text
                                        : "(none)";

                                    Check(
                                        "Transactions grid contains test transaction",
                                        transactionsGrid is not null && GridContains(transactionsGrid, description),
                                        $"rows={transactionsGrid?.Rows.Count} items={txDataSourceCount} status={statusText}");
                                    Check(
                                        "Transactions grid contains transfer",
                                        transactionsGrid is not null &&
                                        GridContains(transactionsGrid, transferDescription),
                                        $"rows={transactionsGrid?.Rows.Count} items={txDataSourceCount} status={statusText}");
                                    Check(
                                        "Transactions grid columns",
                                        transactionsGrid?.Columns.Count == 7,
                                        $"columns={transactionsGrid?.Columns.Count}");

                                    tabs.SelectedIndex = 1;
                                }

                                step = 2;
                                break;

                            case 2:
                                Capture(form, "ledger-ui-categories.png");
                                Check("Categories tab renders", true);

                                if (tabs is not null && statusStrip is not null)
                                {
                                    var categoriesGrid = FindGrid(tabs.TabPages[1]);
                                    Check(
                                        "Categories grid contains test category",
                                        categoriesGrid is not null && GridContains(categoriesGrid, category.Name),
                                        $"rows={categoriesGrid?.Rows.Count}");
                                    Check(
                                        "Categories grid columns",
                                        categoriesGrid?.Columns.Count == 3,
                                        $"columns={categoriesGrid?.Columns.Count}");
                                }

                                timer.Stop();
                                step = 3;
                                form.Close();
                                break;
                        }
                    }
                    catch (Exception exception)
                    {
                        timer.Stop();
                        uiException = exception;
                        form.Close();
                    }
                };

                form.Shown += (_, _) => timer.Start();
                Application.Run(form);

                Check("MainForm shows and closes", step == 3 && uiException is null, $"step={step}");
            }
            catch (Exception exception)
            {
                uiException = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (uiException is not null)
        {
            Console.WriteLine($"FAIL | UI thread exception | {uiException}");
            _failures++;
        }
    }

    private static void Capture(Form form, string fileName)
    {
        if (!_captureScreenshots)
        {
            return;
        }

        Directory.CreateDirectory(_screenshotDirectory);

        var path = Path.Combine(_screenshotDirectory, fileName);

        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
        bitmap.Save(path, ImageFormat.Png);

        Console.WriteLine($"INFO | screenshot saved | {path}");
    }

    private static bool GridContains(DataGridView grid, string value)
    {
        return grid.Rows
            .Cast<DataGridViewRow>()
            .Any(row => row.Cells
                .Cast<DataGridViewCell>()
                .Any(cell => string.Equals(
                    cell.Value?.ToString(),
                    value,
                    StringComparison.Ordinal)));
    }

    private static DataGridView? FindGrid(Control control)
    {
        if (control is DataGridView grid)
        {
            return grid;
        }

        foreach (Control child in control.Controls)
        {
            var found = FindGrid(child);

            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ledger.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) &&
                i + 1 < args.Length &&
                !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return args[i + 1];
            }

            if (args[i].StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase))
            {
                return args[i][(name.Length + 1)..];
            }
        }

        return null;
    }

    private static void Check(string name, bool condition, string? detail = null)
    {
        if (!condition)
        {
            _failures++;
        }

        var suffix = detail is null ? string.Empty : $" | {detail}";
        Console.WriteLine($"{(condition ? "PASS" : "FAIL")} | {name}{suffix}");
        Console.Out.Flush();
    }
}
