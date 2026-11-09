using System.Net;
using Ledger.Api.Contracts.Accounts;
using Ledger.Api.Contracts.Dashboard;
using Ledger.Api.Domain;

namespace Ledger.Api.Tests;

[Collection("api")]
public sealed class DashboardTests : ApiTestBase
{
    public DashboardTests(LedgerApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Dashboard_ReflectsIncomeExpenseAndTransfer()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var month = new DateOnly(today.Year, today.Month, 1);
        var baseline = await GetDashboardAsync(month);

        var source = await CreateAccountAsync();
        var destination = await CreateAccountAsync();
        var incomeCategory = await CreateCategoryAsync(kind: CategoryKind.Income);
        var expenseCategory = await CreateCategoryAsync(kind: CategoryKind.Expense);

        var income = await CreateTransactionAsync(
            source.Id,
            incomeCategory.Id,
            TransactionType.Income,
            1000m,
            today);
        var expense = await CreateTransactionAsync(
            source.Id,
            expenseCategory.Id,
            TransactionType.Expense,
            250m,
            today);
        var transfer = await CreateTransferAsync(source.Id, destination.Id, 100m, today);

        var dashboard = await GetDashboardAsync(month);

        Assert.Equal(month, dashboard.Month);
        Assert.Equal("ARS", dashboard.Currency);
        Assert.Equal(baseline.TotalBalance + 750m, dashboard.TotalBalance);
        Assert.Equal(baseline.MonthIncome + 1000m, dashboard.MonthIncome);
        Assert.Equal(baseline.MonthExpenses + 250m, dashboard.MonthExpenses);

        Assert.Equal(
            650m,
            Assert.Single(dashboard.AccountBalances.Where(account => account.AccountId == source.Id)).Balance);
        Assert.Equal(
            100m,
            Assert.Single(dashboard.AccountBalances.Where(account => account.AccountId == destination.Id)).Balance);

        Assert.Contains(dashboard.RecentTransactions, transaction => transaction.Id == income.Id);
        Assert.Contains(dashboard.RecentTransactions, transaction => transaction.Id == expense.Id);
        Assert.Contains(dashboard.RecentTransactions, transaction => transaction.Id == transfer.OutTransaction.Id);
        Assert.Contains(dashboard.RecentTransactions, transaction => transaction.Id == transfer.InTransaction.Id);

        var trend = Assert.Single(dashboard.MonthlyTrend.Where(entry => entry.Month == month));
        var baselineTrend = Assert.Single(baseline.MonthlyTrend.Where(entry => entry.Month == month));
        Assert.Equal(baselineTrend.Income + 1000m, trend.Income);
        Assert.Equal(baselineTrend.Expenses + 250m, trend.Expenses);

        using var accountsResponse = await Client.GetAsync("api/v1/accounts");
        Assert.Equal(HttpStatusCode.OK, accountsResponse.StatusCode);
        var accounts = await ReadAsync<IReadOnlyList<AccountResponse>>(accountsResponse);
        Assert.Equal(accounts.Sum(account => account.Balance), dashboard.TotalBalance);
    }

    [Fact]
    public async Task Dashboard_SelectedMonth_OnlyIncludesTransactionsFromThatMonth()
    {
        var january = new DateOnly(2024, 1, 1);
        var february = new DateOnly(2024, 2, 1);
        var januaryBaseline = await GetDashboardAsync(january);
        var februaryBaseline = await GetDashboardAsync(february);

        var account = await CreateAccountAsync();
        var incomeCategory = await CreateCategoryAsync(kind: CategoryKind.Income);
        var expenseCategory = await CreateCategoryAsync(kind: CategoryKind.Expense);

        await CreateTransactionAsync(
            account.Id,
            incomeCategory.Id,
            TransactionType.Income,
            100m,
            january.AddDays(14));
        await CreateTransactionAsync(
            account.Id,
            expenseCategory.Id,
            TransactionType.Expense,
            40m,
            january.AddDays(15));
        await CreateTransactionAsync(
            account.Id,
            incomeCategory.Id,
            TransactionType.Income,
            200m,
            february.AddDays(9));

        var januaryDashboard = await GetDashboardAsync(january);
        var februaryDashboard = await GetDashboardAsync(february);

        Assert.Equal(january, januaryDashboard.Month);
        Assert.Equal(januaryBaseline.MonthIncome + 100m, januaryDashboard.MonthIncome);
        Assert.Equal(januaryBaseline.MonthExpenses + 40m, januaryDashboard.MonthExpenses);

        Assert.Equal(february, februaryDashboard.Month);
        Assert.Equal(februaryBaseline.MonthIncome + 200m, februaryDashboard.MonthIncome);
        Assert.Equal(februaryBaseline.MonthExpenses, februaryDashboard.MonthExpenses);

        Assert.Contains(
            januaryDashboard.ExpensesByCategory,
            category => category.CategoryId == expenseCategory.Id && category.Amount == 40m);
        Assert.DoesNotContain(
            februaryDashboard.ExpensesByCategory,
            category => category.CategoryId == expenseCategory.Id);
    }

    [Fact]
    public async Task Dashboard_ExcludesArchivedTransactionsAndAccounts()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var month = new DateOnly(today.Year, today.Month, 1);
        var baseline = await GetDashboardAsync(month);

        var activeAccount = await CreateAccountAsync();
        var archivedAccount = await CreateAccountAsync();
        var incomeCategory = await CreateCategoryAsync(kind: CategoryKind.Income);

        var activeIncome = await CreateTransactionAsync(
            activeAccount.Id,
            incomeCategory.Id,
            TransactionType.Income,
            50m,
            today);
        var archivedAccountIncome = await CreateTransactionAsync(
            archivedAccount.Id,
            incomeCategory.Id,
            TransactionType.Income,
            70m,
            today);
        var archivedTransaction = await CreateTransactionAsync(
            activeAccount.Id,
            incomeCategory.Id,
            TransactionType.Income,
            30m,
            today);

        using var archiveAccountResponse = await Client.DeleteAsync(
            $"api/v1/accounts/{archivedAccount.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveAccountResponse.StatusCode);

        using var archiveTransactionResponse = await Client.DeleteAsync(
            $"api/v1/transactions/{archivedTransaction.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveTransactionResponse.StatusCode);

        var dashboard = await GetDashboardAsync(month);

        Assert.Equal(baseline.TotalBalance + 50m, dashboard.TotalBalance);
        Assert.Equal(baseline.MonthIncome + 50m, dashboard.MonthIncome);
        Assert.DoesNotContain(
            dashboard.AccountBalances,
            account => account.AccountId == archivedAccount.Id);
        Assert.DoesNotContain(
            dashboard.RecentTransactions,
            transaction => transaction.Id == archivedAccountIncome.Id);
        Assert.DoesNotContain(
            dashboard.RecentTransactions,
            transaction => transaction.Id == archivedTransaction.Id);
        Assert.Contains(
            dashboard.RecentTransactions,
            transaction => transaction.Id == activeIncome.Id);
    }

    [Fact]
    public async Task Dashboard_ExpensesByCategory_GroupsOnlyExpenseTransactions()
    {
        var month = new DateOnly(2024, 4, 1);
        var baseline = await GetDashboardAsync(month);

        var account = await CreateAccountAsync();
        var firstCategory = await CreateCategoryAsync(kind: CategoryKind.Expense);
        var secondCategory = await CreateCategoryAsync(kind: CategoryKind.Expense);
        var incomeCategory = await CreateCategoryAsync(kind: CategoryKind.Income);

        await CreateTransactionAsync(
            account.Id,
            firstCategory.Id,
            TransactionType.Expense,
            30m,
            month.AddDays(4));
        await CreateTransactionAsync(
            account.Id,
            secondCategory.Id,
            TransactionType.Expense,
            20m,
            month.AddDays(5));
        await CreateTransactionAsync(
            account.Id,
            incomeCategory.Id,
            TransactionType.Income,
            100m,
            month.AddDays(6));

        var dashboard = await GetDashboardAsync(month);

        Assert.Equal(baseline.MonthExpenses + 50m, dashboard.MonthExpenses);
        Assert.Equal(
            30m,
            Assert.Single(
                dashboard.ExpensesByCategory.Where(category => category.CategoryId == firstCategory.Id)).Amount);
        Assert.Equal(
            20m,
            Assert.Single(
                dashboard.ExpensesByCategory.Where(category => category.CategoryId == secondCategory.Id)).Amount);
        Assert.DoesNotContain(
            dashboard.ExpensesByCategory,
            category => category.CategoryId == incomeCategory.Id);
    }

    [Fact]
    public async Task Dashboard_Trend_HasSixMonthsEndingAtSelectedMonth()
    {
        var month = new DateOnly(2024, 5, 1);

        var dashboard = await GetDashboardAsync(month);

        Assert.Equal(6, dashboard.MonthlyTrend.Count);
        Assert.Equal(month.AddMonths(-5), dashboard.MonthlyTrend[0].Month);
        Assert.Equal(month, dashboard.MonthlyTrend[^1].Month);
        Assert.Equal(dashboard.MonthIncome, dashboard.MonthlyTrend[^1].Income);
        Assert.Equal(dashboard.MonthExpenses, dashboard.MonthlyTrend[^1].Expenses);
    }

    [Fact]
    public async Task Dashboard_InvalidMonth_ReturnsBadRequest()
    {
        using var response = await Client.GetAsync("api/v1/dashboard?month=not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
