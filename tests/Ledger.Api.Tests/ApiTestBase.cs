using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ledger.Api.Contracts.Accounts;
using Ledger.Api.Contracts.Categories;
using Ledger.Api.Contracts.Transactions;
using Ledger.Api.Contracts.Transfers;
using Ledger.Api.Domain;

namespace Ledger.Api.Tests;

public abstract class ApiTestBase : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected ApiTestBase(LedgerApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected LedgerApiFactory Factory { get; }

    protected HttpClient Client { get; }

    public void Dispose()
    {
        Client.Dispose();
    }

    protected static string Unique(string prefix)
    {
        return $"[TEST] {prefix} {Guid.NewGuid():N}";
    }

    protected async Task<AccountResponse> CreateAccountAsync(
        string? name = null,
        AccountType type = AccountType.Wallet)
    {
        using var response = await PostJsonAsync(
            "api/v1/accounts",
            new CreateAccountRequest { Name = name ?? Unique("Account"), Type = type });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<AccountResponse>(response);
    }

    protected async Task<CategoryResponse> CreateCategoryAsync(
        string? name = null,
        CategoryKind kind = CategoryKind.Expense)
    {
        using var response = await PostJsonAsync(
            "api/v1/categories",
            new CreateCategoryRequest { Name = name ?? Unique("Category"), Kind = kind });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<CategoryResponse>(response);
    }

    protected async Task<TransactionResponse> CreateTransactionAsync(
        Guid accountId,
        Guid categoryId,
        TransactionType type,
        decimal amount,
        DateOnly? date = null,
        string? description = null)
    {
        using var response = await PostJsonAsync(
            "api/v1/transactions",
            new CreateTransactionRequest
            {
                AccountId = accountId,
                CategoryId = categoryId,
                Type = type,
                Amount = amount,
                Date = date ?? DateOnly.FromDateTime(DateTime.Today),
                Description = description ?? Unique("Transaction")
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<TransactionResponse>(response);
    }

    protected async Task<TransferResponse> CreateTransferAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        DateOnly? date = null,
        string? description = null)
    {
        using var response = await PostJsonAsync(
            "api/v1/transfers",
            new CreateTransferRequest
            {
                FromAccountId = fromAccountId,
                ToAccountId = toAccountId,
                Amount = amount,
                Date = date ?? DateOnly.FromDateTime(DateTime.Today),
                Description = description ?? Unique("Transfer")
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<TransferResponse>(response);
    }

    protected async Task<AccountResponse> GetAccountAsync(Guid accountId)
    {
        using var response = await Client.GetAsync($"api/v1/accounts/{accountId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<AccountResponse>(response);
    }

    protected async Task<HttpResponseMessage> PostJsonAsync<T>(string path, T value)
    {
        using var content = JsonContent.Create(value, options: JsonOptions);
        return await Client.PostAsync(path, content);
    }

    protected async Task<HttpResponseMessage> PutJsonAsync<T>(string path, T value)
    {
        using var content = JsonContent.Create(value, options: JsonOptions);
        return await Client.PutAsync(path, content);
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);

        return result ?? throw new InvalidOperationException("The API returned an empty response.");
    }

    protected static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadAsStringAsync();
    }
}
