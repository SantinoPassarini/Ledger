using System.Net;
using Ledger.Api.Contracts.Budgets;
using Ledger.Api.Domain;

namespace Ledger.Api.Tests;

[Collection("api")]
public sealed class BudgetsTests : ApiTestBase
{
    public BudgetsTests(LedgerApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateBudget_ReturnsNormalizedMonthAndTotals()
    {
        var month = new DateOnly(2024, 6, 18);
        var category = await CreateCategoryAsync(kind: CategoryKind.Expense);

        var budget = await CreateBudgetAsync(category.Id, month, 500m);

        Assert.Equal(new DateOnly(2024, 6, 1), budget.Month);
        Assert.Equal(category.Id, budget.CategoryId);
        Assert.Equal(category.Name, budget.CategoryName);
        Assert.Equal(500m, budget.Amount);
        Assert.Equal(0m, budget.Spent);
        Assert.Equal(500m, budget.Remaining);
        Assert.Equal(0m, budget.PercentUsed);
        Assert.False(budget.IsArchived);
    }

    [Fact]
    public async Task BudgetSpent_CountsOnlyExpensesForSelectedMonth()
    {
        var month = new DateOnly(2024, 7, 1);
        var account = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Expense);
        var incomeCategory = await CreateCategoryAsync(kind: CategoryKind.Income);
        await CreateBudgetAsync(category.Id, month, 100m);

        await CreateTransactionAsync(
            account.Id,
            category.Id,
            TransactionType.Expense,
            30m,
            month.AddDays(4));
        await CreateTransactionAsync(
            account.Id,
            category.Id,
            TransactionType.Expense,
            20m,
            month.AddMonths(1).AddDays(-1));
        await CreateTransactionAsync(
            account.Id,
            incomeCategory.Id,
            TransactionType.Income,
            500m,
            month.AddDays(9));
        await CreateTransactionAsync(
            account.Id,
            category.Id,
            TransactionType.Expense,
            40m,
            month.AddMonths(1));
        await CreateTransactionAsync(
            account.Id,
            category.Id,
            TransactionType.Expense,
            10m,
            month.AddDays(-1));

        var budgets = await GetBudgetsAsync(month);
        var budget = Assert.Single(
            budgets.Where(candidate => candidate.CategoryId == category.Id));

        Assert.Equal(50m, budget.Spent);
        Assert.Equal(50m, budget.Remaining);
        Assert.Equal(50m, budget.PercentUsed);
    }

    [Fact]
    public async Task BudgetSpent_ExcludesArchivedTransactionsAndArchivedAccounts()
    {
        var month = new DateOnly(2024, 9, 1);
        var activeAccount = await CreateAccountAsync();
        var archivedAccount = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Expense);
        await CreateBudgetAsync(category.Id, month, 100m);

        await CreateTransactionAsync(
            activeAccount.Id,
            category.Id,
            TransactionType.Expense,
            30m,
            month.AddDays(2));
        var archivedTransaction = await CreateTransactionAsync(
            activeAccount.Id,
            category.Id,
            TransactionType.Expense,
            20m,
            month.AddDays(3));
        await CreateTransactionAsync(
            archivedAccount.Id,
            category.Id,
            TransactionType.Expense,
            25m,
            month.AddDays(4));

        using var archiveTransactionResponse = await Client.DeleteAsync(
            $"api/v1/transactions/{archivedTransaction.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveTransactionResponse.StatusCode);

        using var archiveAccountResponse = await Client.DeleteAsync(
            $"api/v1/accounts/{archivedAccount.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveAccountResponse.StatusCode);

        var budgets = await GetBudgetsAsync(month);
        var budget = Assert.Single(
            budgets.Where(candidate => candidate.CategoryId == category.Id));

        Assert.Equal(30m, budget.Spent);
        Assert.Equal(70m, budget.Remaining);
        Assert.Equal(30m, budget.PercentUsed);
    }

    [Fact]
    public async Task CreateBudget_ForIncomeCategory_ReturnsValidationProblem()
    {
        var category = await CreateCategoryAsync(kind: CategoryKind.Income);

        using var response = await PostJsonAsync(
            "api/v1/budgets",
            new CreateBudgetRequest
            {
                CategoryId = category.Id,
                Month = new DateOnly(2024, 10, 1),
                Amount = 100m
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBudget_DuplicateActive_ReturnsConflict()
    {
        var month = new DateOnly(2024, 11, 1);
        var category = await CreateCategoryAsync(kind: CategoryKind.Expense);
        await CreateBudgetAsync(category.Id, month, 100m);

        using var response = await PostJsonAsync(
            "api/v1/budgets",
            new CreateBudgetRequest
            {
                CategoryId = category.Id,
                Month = month,
                Amount = 200m
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateBudget_AfterArchiveRestoresExistingBudget()
    {
        var month = new DateOnly(2024, 12, 1);
        var category = await CreateCategoryAsync(kind: CategoryKind.Expense);
        var created = await CreateBudgetAsync(category.Id, month, 100m);

        using var archiveResponse = await Client.DeleteAsync($"api/v1/budgets/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var visible = await GetBudgetsAsync(month);
        Assert.DoesNotContain(visible, budget => budget.CategoryId == category.Id);

        var all = await GetBudgetsAsync(month, includeArchived: true);
        var archived = Assert.Single(
            all.Where(budget => budget.CategoryId == category.Id));
        Assert.True(archived.IsArchived);

        var restored = await CreateBudgetAsync(category.Id, month, 250m);

        Assert.Equal(created.Id, restored.Id);
        Assert.Equal(250m, restored.Amount);
        Assert.False(restored.IsArchived);

        var restoredList = await GetBudgetsAsync(month, includeArchived: true);
        var restoredBudget = Assert.Single(
            restoredList.Where(budget => budget.CategoryId == category.Id));
        Assert.Equal(created.Id, restoredBudget.Id);
        Assert.False(restoredBudget.IsArchived);
    }

    [Fact]
    public async Task UpdateBudget_RecalculatesTotalsAndRejectsArchivedBudgets()
    {
        var month = new DateOnly(2025, 1, 1);
        var account = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Expense);
        var budget = await CreateBudgetAsync(category.Id, month, 100m);
        await CreateTransactionAsync(
            account.Id,
            category.Id,
            TransactionType.Expense,
            60m,
            month.AddDays(5));

        using var updateResponse = await PutJsonAsync(
            $"api/v1/budgets/{budget.Id}",
            new UpdateBudgetRequest { Amount = 80m });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadAsync<BudgetResponse>(updateResponse);
        Assert.Equal(80m, updated.Amount);
        Assert.Equal(60m, updated.Spent);
        Assert.Equal(20m, updated.Remaining);
        Assert.Equal(75m, updated.PercentUsed);

        using var archiveResponse = await Client.DeleteAsync($"api/v1/budgets/{budget.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        using var archivedUpdateResponse = await PutJsonAsync(
            $"api/v1/budgets/{budget.Id}",
            new UpdateBudgetRequest { Amount = 200m });

        Assert.Equal(HttpStatusCode.Conflict, archivedUpdateResponse.StatusCode);
    }

    [Fact]
    public async Task GetBudgets_FiltersByMonthAndNormalizesTheQuery()
    {
        var firstMonth = new DateOnly(2025, 2, 1);
        var secondMonth = new DateOnly(2025, 3, 1);
        var firstCategory = await CreateCategoryAsync(kind: CategoryKind.Expense);
        var secondCategory = await CreateCategoryAsync(kind: CategoryKind.Expense);
        await CreateBudgetAsync(firstCategory.Id, firstMonth, 100m);
        await CreateBudgetAsync(secondCategory.Id, secondMonth, 200m);

        var firstMonthBudgets = await GetBudgetsAsync(firstMonth);
        Assert.Contains(firstMonthBudgets, budget => budget.CategoryId == firstCategory.Id);
        Assert.DoesNotContain(firstMonthBudgets, budget => budget.CategoryId == secondCategory.Id);

        var normalized = await GetBudgetsAsync(firstMonth.AddDays(20));
        Assert.Contains(normalized, budget => budget.CategoryId == firstCategory.Id);
        Assert.DoesNotContain(normalized, budget => budget.CategoryId == secondCategory.Id);
    }

    [Fact]
    public async Task InvalidBudgetAmount_ReturnsValidationProblem()
    {
        var category = await CreateCategoryAsync(kind: CategoryKind.Expense);

        using var zeroResponse = await PostJsonAsync(
            "api/v1/budgets",
            new CreateBudgetRequest
            {
                CategoryId = category.Id,
                Month = new DateOnly(2025, 4, 1),
                Amount = 0m
            });

        Assert.Equal(HttpStatusCode.BadRequest, zeroResponse.StatusCode);

        using var decimalsResponse = await PostJsonAsync(
            "api/v1/budgets",
            new CreateBudgetRequest
            {
                CategoryId = category.Id,
                Month = new DateOnly(2025, 4, 1),
                Amount = 10.123m
            });

        Assert.Equal(HttpStatusCode.BadRequest, decimalsResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidBudgetMonth_ReturnsBadRequest()
    {
        using var response = await Client.GetAsync("api/v1/budgets?month=not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBudgetById_ReturnsBudget()
    {
        var month = new DateOnly(2025, 5, 1);
        var category = await CreateCategoryAsync(kind: CategoryKind.Expense);
        var budget = await CreateBudgetAsync(category.Id, month, 300m);

        using var response = await Client.GetAsync($"api/v1/budgets/{budget.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync<BudgetResponse>(response);
        Assert.Equal(budget.Id, body.Id);
        Assert.Equal(category.Name, body.CategoryName);
        Assert.Equal(300m, body.Amount);
    }
}
