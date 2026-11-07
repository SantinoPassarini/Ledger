using System.Net;
using Ledger.Api.Contracts.Accounts;
using Ledger.Api.Contracts.Common;
using Ledger.Api.Contracts.Transactions;
using Ledger.Api.Domain;

namespace Ledger.Api.Tests;

[Collection("api")]
public sealed class TransactionsTests : ApiTestBase
{
    public TransactionsTests(LedgerApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task IncomeAndExpense_UpdateAccountBalance()
    {
        var account = await CreateAccountAsync();
        var incomeCategory = await CreateCategoryAsync(kind: CategoryKind.Income);
        var expenseCategory = await CreateCategoryAsync(kind: CategoryKind.Expense);

        await CreateTransactionAsync(
            account.Id,
            incomeCategory.Id,
            TransactionType.Income,
            1000.50m);
        await CreateTransactionAsync(
            account.Id,
            expenseCategory.Id,
            TransactionType.Expense,
            250.25m);

        var balance = await GetBalanceAsync(account.Id);
        Assert.Equal(750.25m, balance);
    }

    [Fact]
    public async Task UpdateTransaction_UpdatesBalance()
    {
        var account = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Income);
        var transaction = await CreateTransactionAsync(
            account.Id,
            category.Id,
            TransactionType.Income,
            100m);

        using var response = await PutJsonAsync(
            $"api/v1/transactions/{transaction.Id}",
            new UpdateTransactionRequest
            {
                AccountId = account.Id,
                CategoryId = category.Id,
                Type = TransactionType.Income,
                Amount = 200.50m,
                Date = transaction.Date,
                Description = Unique("Updated transaction")
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(200.50m, await GetBalanceAsync(account.Id));
    }

    [Fact]
    public async Task ArchiveTransaction_RemovesItFromBalance()
    {
        var account = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Income);
        var transaction = await CreateTransactionAsync(
            account.Id,
            category.Id,
            TransactionType.Income,
            100m);

        using var response = await Client.DeleteAsync($"api/v1/transactions/{transaction.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0m, await GetBalanceAsync(account.Id));
    }

    [Fact]
    public async Task FiltersAndSearch_ReturnOnlyMatchingTransaction()
    {
        var account = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Income);
        var description = Unique("Searchable transaction");
        var transaction = await CreateTransactionAsync(
            account.Id,
            category.Id,
            TransactionType.Income,
            50m,
            description: description);
        await CreateTransactionAsync(
            account.Id,
            category.Id,
            TransactionType.Income,
            75m,
            description: Unique("Other transaction"));

        var path = $"api/v1/transactions?accountId={account.Id}&search={Uri.EscapeDataString("Searchable")}";
        using var response = await Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResponse<TransactionResponse>>(response);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(transaction.Id, page.Items[0].Id);
        Assert.Equal(description, page.Items[0].Description);
    }

    [Fact]
    public async Task Pagination_ReturnsRequestedPage()
    {
        var account = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Expense);

        for (var i = 0; i < 3; i++)
        {
            await CreateTransactionAsync(
                account.Id,
                category.Id,
                TransactionType.Expense,
                10m + i,
                DateOnly.FromDateTime(DateTime.Today.AddDays(-i)));
        }

        using var response = await Client.GetAsync(
            $"api/v1/transactions?accountId={account.Id}&page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResponse<TransactionResponse>>(response);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task ArchivedTransaction_IsHiddenByDefaultAndVisibleWithIncludeArchived()
    {
        var account = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Income);
        var transaction = await CreateTransactionAsync(
            account.Id,
            category.Id,
            TransactionType.Income,
            10m);

        using var archiveResponse = await Client.DeleteAsync($"api/v1/transactions/{transaction.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        using var visibleResponse = await Client.GetAsync(
            $"api/v1/transactions?accountId={account.Id}");
        var visible = await ReadAsync<PagedResponse<TransactionResponse>>(visibleResponse);
        Assert.Equal(0, visible.TotalCount);

        using var allResponse = await Client.GetAsync(
            $"api/v1/transactions?accountId={account.Id}&includeArchived=true");
        var all = await ReadAsync<PagedResponse<TransactionResponse>>(allResponse);
        Assert.Equal(1, all.TotalCount);
        Assert.True(all.Items[0].IsArchived);
    }

    [Fact]
    public async Task TransferTypes_AreRejectedByTransactionEndpoint()
    {
        var account = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Expense);

        using var response = await PostJsonAsync(
            "api/v1/transactions",
            new CreateTransactionRequest
            {
                AccountId = account.Id,
                CategoryId = category.Id,
                Type = TransactionType.TransferOut,
                Amount = 10m,
                Date = DateOnly.FromDateTime(DateTime.Today)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CategoryKindMismatch_ReturnsValidationProblem()
    {
        var account = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Expense);

        using var response = await PostJsonAsync(
            "api/v1/transactions",
            new CreateTransactionRequest
            {
                AccountId = account.Id,
                CategoryId = category.Id,
                Type = TransactionType.Income,
                Amount = 10m,
                Date = DateOnly.FromDateTime(DateTime.Today)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TransactionOnArchivedAccount_ReturnsConflict()
    {
        var account = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Income);

        using var archiveResponse = await Client.DeleteAsync($"api/v1/accounts/{account.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        using var response = await PostJsonAsync(
            "api/v1/transactions",
            new CreateTransactionRequest
            {
                AccountId = account.Id,
                CategoryId = category.Id,
                Type = TransactionType.Income,
                Amount = 10m,
                Date = DateOnly.FromDateTime(DateTime.Today)
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task FromAfterTo_ReturnsValidationProblem()
    {
        var from = DateOnly.FromDateTime(DateTime.Today);
        var to = from.AddDays(-1);

        using var response = await Client.GetAsync(
            $"api/v1/transactions?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<decimal> GetBalanceAsync(Guid accountId)
    {
        using var response = await Client.GetAsync($"api/v1/accounts/{accountId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await ReadAsync<AccountResponse>(response);
        return account.Balance;
    }
}
