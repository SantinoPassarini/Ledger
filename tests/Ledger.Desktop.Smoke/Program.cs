using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Reflection;
using System.Windows.Forms;
using Ledger.Desktop;
using Ledger.Desktop.Api;
using Ledger.Desktop.Dialogs;
using Ledger.Desktop.Models;
using Ledger.Desktop.Ui;

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

        var identifier = GetOption(args, "--identifier")
            ?? GetOption(args, "--email")
            ?? Environment.GetEnvironmentVariable("LEDGER_SMOKE_IDENTIFIER")
            ?? Environment.GetEnvironmentVariable("LEDGER_OWNER_EMAIL");
        var password = GetOption(args, "--password")
            ?? Environment.GetEnvironmentVariable("LEDGER_SMOKE_PASSWORD")
            ?? Environment.GetEnvironmentVariable("LEDGER_OWNER_PASSWORD");

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine(
                "FAIL | credentials | pass --identifier and --password or set LEDGER_SMOKE_IDENTIFIER and LEDGER_SMOKE_PASSWORD");
            Console.WriteLine("FAILURES=1");
            return 1;
        }

        using var api = new LedgerApiClient(apiBaseUrl);

        using (var anonymous = new HttpClient { BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/") })
        {
            using var response = await anonymous.GetAsync("api/v1/accounts");
            Check(
                "unauthenticated request is rejected",
                response.StatusCode == HttpStatusCode.Unauthorized,
                $"status={(int)response.StatusCode}");
        }

        try
        {
            var user = await api.LoginAsync(identifier, password);
            Check(
                "login",
                api.IsAuthenticated && !string.IsNullOrWhiteSpace(user.Username),
                user.Username);
        }
        catch (Exception exception)
        {
            Check("login", false, exception.Message);
            Console.WriteLine($"FAILURES={_failures}");
            return 1;
        }

        var suffix = Guid.NewGuid().ToString("N");
        var accountName = $"[TEST] Desktop {suffix}";
        var transferAccountName = $"[TEST] Desktop transfer {suffix}";
        var categoryName = $"[TEST] Desktop category {suffix}";
        var budgetCategoryName = $"[TEST] Desktop budget category {suffix}";
        var description = $"[TEST] desktop smoke {suffix}";
        var transferDescription = $"[TEST] desktop transfer {suffix}";
        AccountModel? account = null;
        AccountModel? transferAccount = null;
        CategoryModel? category = null;
        CategoryModel? budgetCategory = null;
        TransactionModel? transaction = null;
        TransferModel? transfer = null;
        BudgetModel? budget = null;

        try
        {
            var accounts = await api.GetAccountsAsync(includeArchived: true);
            Check("GET accounts", true, $"{accounts.Count} account(s)");

            var categories = await api.GetCategoriesAsync(includeArchived: true);
            Check("GET categories", categories.Count >= 15, $"{categories.Count} category(ies)");

            var firstPage = await api.GetTransactionsAsync(new TransactionQuery { PageSize = 5 });
            Check("GET transactions", firstPage.PageSize == 5, $"total={firstPage.TotalCount}");

            var currentMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
            var dashboardBaseline = await api.GetDashboardAsync(currentMonth);
            Check(
                "GET dashboard",
                dashboardBaseline.Month == currentMonth && dashboardBaseline.MonthlyTrend.Count == 6,
                $"{dashboardBaseline.Month:yyyy-MM} trend={dashboardBaseline.MonthlyTrend.Count}");

            account = await api.CreateAccountAsync(new AccountInput(accountName, AccountType.Wallet));
            Check("create account", account.Balance == 0m, account.Id.ToString());

            transferAccount = await api.CreateAccountAsync(
                new AccountInput(transferAccountName, AccountType.Wallet));
            Check("create transfer account", transferAccount.Balance == 0m, transferAccount.Id.ToString());

            category = await api.CreateCategoryAsync(
                new CategoryInput(categoryName, CategoryKind.Income));
            Check("create category", category.Kind == CategoryKind.Income);

            budgetCategory = await api.CreateCategoryAsync(
                new CategoryInput(budgetCategoryName, CategoryKind.Expense));
            Check("create budget category", budgetCategory.Kind == CategoryKind.Expense);

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

            var dashboard = await api.GetDashboardAsync(currentMonth);
            Check(
                "dashboard total balance reflects activity",
                dashboard.TotalBalance == dashboardBaseline.TotalBalance + 200.50m,
                $"expected={dashboardBaseline.TotalBalance + 200.50m:N2} actual={dashboard.TotalBalance:N2}");
            Check(
                "dashboard month income reflects activity",
                dashboard.MonthIncome == dashboardBaseline.MonthIncome + 200.50m,
                $"expected={dashboardBaseline.MonthIncome + 200.50m:N2} actual={dashboard.MonthIncome:N2}");
            Check(
                "dashboard account balances",
                dashboard.AccountBalances.Single(candidate => candidate.AccountId == account.Id).Balance == 150.25m &&
                dashboard.AccountBalances
                    .Single(candidate => candidate.AccountId == transferAccount.Id).Balance == 50.25m,
                $"{dashboard.AccountBalances.Count} account(s)");
            Check(
                "dashboard recent includes transfer",
                dashboard.RecentTransactions.Any(candidate =>
                    candidate.TransferGroupId == transfer.TransferGroupId),
                $"{dashboard.RecentTransactions.Count} recent transaction(s)");

            var transferSearch = await api.GetTransactionsAsync(
                new TransactionQuery
                {
                    Search = transferDescription,
                    IncludeArchived = true
                });
            Check("search transfer legs", transferSearch.TotalCount == 2, transferSearch.TotalCount.ToString());

            try
            {
                await api.CreateTransferAsync(
                    new TransferInput(
                        account.Id,
                        transferAccount.Id,
                        1000m,
                        DateOnly.FromDateTime(DateTime.Today),
                        $"[TEST] insufficient {suffix}"));
                Check("transfer rejects insufficient funds", false, "transfer was created");
            }
            catch (ApiException exception)
            {
                Check(
                    "transfer rejects insufficient funds",
                    exception.StatusCode == 409,
                    $"status={exception.StatusCode}");
            }

            var afterRejectedTransfer = await api.GetAccountsAsync(includeArchived: true);
            Check(
                "balances unchanged after rejected transfer",
                afterRejectedTransfer.Single(candidate => candidate.Id == account.Id).Balance == 150.25m &&
                afterRejectedTransfer.Single(candidate => candidate.Id == transferAccount.Id).Balance == 50.25m,
                "no balance change");

            budget = await api.CreateBudgetAsync(
                new BudgetInput(budgetCategory.Id, currentMonth, 500m));
            Check(
                "create budget",
                budget.Month == currentMonth && budget.Amount == 500m && budget.Spent == 0m,
                $"{budget.Month:yyyy-MM} amount={budget.Amount:N2} spent={budget.Spent:N2}");

            var budgets = await api.GetBudgetsAsync(currentMonth);
            Check(
                "GET budgets",
                budgets.Any(candidate => candidate.Id == budget.Id),
                $"{budgets.Count} budget(s)");

            var updatedBudget = await api.UpdateBudgetAsync(
                budget.Id,
                new BudgetUpdateInput(450m));
            Check("update budget", updatedBudget.Amount == 450m && updatedBudget.Remaining == 450m);

            RunUiSmoke(
                api,
                apiBaseUrl,
                account,
                transferAccount,
                category,
                budgetCategory,
                description,
                transferDescription);

            await api.ArchiveBudgetAsync(budget.Id);

            var visibleBudgets = await api.GetBudgetsAsync(currentMonth);
            Check(
                "archived budget hidden by default",
                visibleBudgets.All(candidate => candidate.Id != budget.Id));

            var allBudgets = await api.GetBudgetsAsync(currentMonth, includeArchived: true);
            Check(
                "archived budget visible with includeArchived",
                allBudgets.Any(candidate => candidate.Id == budget.Id && candidate.IsArchived));

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
            await api.ArchiveCategoryAsync(budgetCategory.Id);

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

            if (budget is not null)
            {
                try
                {
                    await api.ArchiveBudgetAsync(budget.Id);
                }
                catch
                {
                }
            }

            if (budgetCategory is not null)
            {
                try
                {
                    await api.ArchiveCategoryAsync(budgetCategory.Id);
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
        CategoryModel budgetCategory,
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

                using (var budgetDialog = new BudgetDialog(
                           "New budget",
                           new[] { budgetCategory },
                           new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1)))
                {
                    budgetDialog.CreateControl();
                    Check("BudgetDialog constructs", true);
                }

                using (var loginForm = new LoginForm(api, apiBaseUrl))
                {
                    loginForm.CreateControl();
                    Check("LoginForm constructs", true);
                }

                using (var registerForm = new RegisterForm(api))
                {
                    registerForm.CreateControl();
                    Check("RegisterForm constructs", true);
                }

                using (var changePasswordDialog = new ChangePasswordDialog(api))
                {
                    changePasswordDialog.CreateControl();
                    Check("ChangePasswordDialog constructs", true);
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
                var userLabel = (Label?)typeof(MainForm)
                    .GetField("_userLabel", BindingFlags.NonPublic | BindingFlags.Instance)?
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
                                Capture(form, "ledger-ui-dashboard.png");
                                Check("Dashboard tab renders", true);
                                Check("MainForm tabs found", tabs is not null);
                                Check("MainForm status strip found", statusStrip is not null);
                                Check(
                                    "MainForm shows signed-in user",
                                    userLabel is not null &&
                                    string.Equals(
                                        userLabel.Text,
                                        api.CurrentUser?.Username,
                                        StringComparison.Ordinal),
                                    userLabel?.Text);

                                if (tabs is not null && statusStrip is not null)
                                {
                                    Check("Layout: tabs below header", tabs.Top >= 64, $"top={tabs.Top}");
                                    Check(
                                        "Layout: tabs above status strip",
                                        tabs.Bottom <= statusStrip.Top,
                                        $"tabsBottom={tabs.Bottom} statusTop={statusStrip.Top}");
                                    Check(
                                        "MainForm has expected tabs",
                                        ContainsTab(tabs, "Dashboard") &&
                                        ContainsTab(tabs, "Accounts") &&
                                        ContainsTab(tabs, "Categories") &&
                                        ContainsTab(tabs, "Budgets") &&
                                        ContainsTab(tabs, "Transactions"),
                                        string.Join(
                                            ", ",
                                            tabs.TabPages.Cast<TabPage>().Select(page => page.Text)));

                                    var dashboardPage = FindTab(tabs, "Dashboard");
                                    Check("Dashboard tab found", dashboardPage is not null);

                                    var totalBalance = FindNamedControl<Label>(
                                        dashboardPage,
                                        "DashboardTotalBalanceValue");
                                    var income = FindNamedControl<Label>(dashboardPage, "DashboardIncomeValue");
                                    var expenses = FindNamedControl<Label>(
                                        dashboardPage,
                                        "DashboardExpensesValue");

                                    Check(
                                        "Dashboard total balance card loaded",
                                        totalBalance is not null && totalBalance.Text != "-",
                                        totalBalance?.Text);
                                    Check(
                                        "Dashboard income card loaded",
                                        income is not null && income.Text != "-",
                                        income?.Text);
                                    Check(
                                        "Dashboard expenses card loaded",
                                        expenses is not null && expenses.Text != "-",
                                        expenses?.Text);

                                    var monthlyChart = FindNamedControl<MonthlyBarChart>(
                                        dashboardPage,
                                        "DashboardMonthlyChart");
                                    var trendGrid = FindNamedControl<DataGridView>(
                                        dashboardPage,
                                        "DashboardTrendGrid");
                                    var recentGrid = FindNamedControl<DataGridView>(
                                        dashboardPage,
                                        "DashboardRecentGrid");

                                    Check(
                                        "Dashboard chart has six months",
                                        monthlyChart?.Data.Count == 6,
                                        $"months={monthlyChart?.Data.Count}");
                                    Check(
                                        "Dashboard chart has activity",
                                        monthlyChart is not null &&
                                        monthlyChart.Data.Any(entry =>
                                            entry.Income > 0m || entry.Expenses > 0m),
                                        $"months={monthlyChart?.Data.Count}");
                                    Check(
                                        "Dashboard trend has six months",
                                        trendGrid?.Rows.Count == 6,
                                        $"rows={trendGrid?.Rows.Count}");
                                    Check(
                                        "Dashboard recent grid contains test transaction",
                                        recentGrid is not null && GridContains(recentGrid, description),
                                        $"rows={recentGrid?.Rows.Count}");
                                    Check(
                                        "Dashboard recent grid contains transfer",
                                        recentGrid is not null &&
                                        GridContains(recentGrid, transferDescription),
                                        $"rows={recentGrid?.Rows.Count}");

                                    SelectTab(tabs, "Accounts");
                                }

                                step = 1;
                                break;

                            case 1:
                                Capture(form, "ledger-ui-accounts.png");
                                Check("Accounts tab renders", true);

                                if (tabs is not null)
                                {
                                    var accountsPage = FindTab(tabs, "Accounts");
                                    var accountsGrid = accountsPage is null ? null : FindGrid(accountsPage);
                                    Check(
                                        "Accounts grid contains test account",
                                        accountsGrid is not null && GridContains(accountsGrid, account.Name),
                                        $"rows={accountsGrid?.Rows.Count}");
                                    Check(
                                        "Accounts grid columns",
                                        accountsGrid?.Columns.Count == 5,
                                        $"columns={accountsGrid?.Columns.Count}");

                                    SelectTab(tabs, "Categories");
                                }

                                step = 2;
                                break;

                            case 2:
                                Capture(form, "ledger-ui-categories.png");
                                Check("Categories tab renders", true);

                                if (tabs is not null)
                                {
                                    var categoriesPage = FindTab(tabs, "Categories");
                                    var categoriesGrid = categoriesPage is null
                                        ? null
                                        : FindGrid(categoriesPage);
                                    Check(
                                        "Categories grid contains test category",
                                        categoriesGrid is not null &&
                                        GridContains(categoriesGrid, category.Name),
                                        $"rows={categoriesGrid?.Rows.Count}");
                                    Check(
                                        "Categories grid contains budget category",
                                        categoriesGrid is not null &&
                                        GridContains(categoriesGrid, budgetCategory.Name),
                                        $"rows={categoriesGrid?.Rows.Count}");
                                    Check(
                                        "Categories grid columns",
                                        categoriesGrid?.Columns.Count == 3,
                                        $"columns={categoriesGrid?.Columns.Count}");

                                    SelectTab(tabs, "Budgets");
                                }

                                step = 3;
                                break;

                            case 3:
                                Capture(form, "ledger-ui-budgets.png");
                                Check("Budgets tab renders", true);

                                if (tabs is not null)
                                {
                                    var budgetsPage = FindTab(tabs, "Budgets");
                                    var budgetsGrid = FindNamedControl<DataGridView>(
                                        budgetsPage,
                                        "BudgetsGrid");
                                    var monthLabel = FindNamedControl<Label>(
                                        budgetsPage,
                                        "BudgetsMonthLabel");

                                    Check(
                                        "Budgets grid contains test budget",
                                        budgetsGrid is not null &&
                                        GridContains(budgetsGrid, budgetCategory.Name),
                                        $"rows={budgetsGrid?.Rows.Count}");
                                    Check(
                                        "Budgets grid columns",
                                        budgetsGrid?.Columns.Count == 7,
                                        $"columns={budgetsGrid?.Columns.Count}");
                                    Check(
                                        "Budgets month label loaded",
                                        monthLabel is not null && monthLabel.Text.Length > 0,
                                        monthLabel?.Text);

                                    SelectTab(tabs, "Transactions");
                                }

                                step = 4;
                                break;

                            case 4:
                                Capture(form, "ledger-ui-transactions.png");
                                Check("Transactions tab renders", true);

                                if (tabs is not null && statusStrip is not null)
                                {
                                    var transactionsPage = FindTab(tabs, "Transactions");
                                    var transactionsGrid = transactionsPage is null
                                        ? null
                                        : FindGrid(transactionsPage);
                                    var txDataSourceCount =
                                        transactionsGrid?.DataSource is System.Collections.ICollection collection
                                            ? collection.Count
                                            : -1;
                                    var statusText = statusStrip.Items.Count > 0
                                        ? statusStrip.Items[0].Text
                                        : "(none)";

                                    Check(
                                        "Transactions grid contains test transaction",
                                        transactionsGrid is not null &&
                                        GridContains(transactionsGrid, description),
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
                                }

                                timer.Stop();
                                step = 5;
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

                Check("MainForm shows and closes", step == 5 && uiException is null, $"step={step}");
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

    private static bool ContainsTab(TabControl tabs, string text)
    {
        return FindTab(tabs, text) is not null;
    }

    private static TabPage? FindTab(TabControl tabs, string text)
    {
        return tabs.TabPages
            .Cast<TabPage>()
            .FirstOrDefault(page => string.Equals(page.Text, text, StringComparison.Ordinal));
    }

    private static void SelectTab(TabControl tabs, string text)
    {
        var page = FindTab(tabs, text);

        if (page is not null)
        {
            tabs.SelectedTab = page;
        }
    }

    private static Control? FindControl(Control? control, string name)
    {
        if (control is null)
        {
            return null;
        }

        if (string.Equals(control.Name, name, StringComparison.Ordinal))
        {
            return control;
        }

        foreach (Control child in control.Controls)
        {
            var found = FindControl(child, name);

            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static T? FindNamedControl<T>(Control? control, string name)
        where T : Control
    {
        return FindControl(control, name) as T;
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
