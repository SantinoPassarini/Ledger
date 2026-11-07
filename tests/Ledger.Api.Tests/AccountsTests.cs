using System.Net;
using Ledger.Api.Contracts.Accounts;
using Ledger.Api.Domain;

namespace Ledger.Api.Tests;

[Collection("api")]
public sealed class AccountsTests : ApiTestBase
{
    public AccountsTests(LedgerApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateAndGetAccount_ReturnsZeroBalance()
    {
        var account = await CreateAccountAsync();

        using var response = await Client.GetAsync($"api/v1/accounts/{account.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync<AccountResponse>(response);
        Assert.Equal(account.Id, body.Id);
        Assert.Equal(account.Name, body.Name);
        Assert.Equal(AccountType.Wallet, body.Type);
        Assert.Equal(0m, body.Balance);
        Assert.False(body.IsArchived);
    }

    [Fact]
    public async Task UpdateAccount_ChangesNameAndType()
    {
        var account = await CreateAccountAsync();

        using var response = await PutJsonAsync(
            $"api/v1/accounts/{account.Id}",
            new UpdateAccountRequest { Name = Unique("Updated account"), Type = AccountType.Bank });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var getResponse = await Client.GetAsync($"api/v1/accounts/{account.Id}");
        var updated = await ReadAsync<AccountResponse>(getResponse);
        Assert.Equal(AccountType.Bank, updated.Type);
        Assert.Contains("[TEST] Updated account", updated.Name);
    }

    [Fact]
    public async Task CreateAccount_WithEmptyName_ReturnsValidationProblem()
    {
        using var response = await PostJsonAsync(
            "api/v1/accounts",
            new CreateAccountRequest { Name = "   ", Type = AccountType.Cash });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ArchivedAccount_IsHiddenByDefaultAndVisibleWithIncludeArchived()
    {
        var account = await CreateAccountAsync();

        using var archiveResponse = await Client.DeleteAsync($"api/v1/accounts/{account.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        using var visibleResponse = await Client.GetAsync("api/v1/accounts");
        var visible = await ReadAsync<List<AccountResponse>>(visibleResponse);
        Assert.DoesNotContain(visible, item => item.Id == account.Id);

        using var allResponse = await Client.GetAsync("api/v1/accounts?includeArchived=true");
        var all = await ReadAsync<List<AccountResponse>>(allResponse);
        Assert.Contains(all, item => item.Id == account.Id && item.IsArchived);
    }

    [Fact]
    public async Task MissingAccount_ReturnsNotFound()
    {
        using var response = await Client.GetAsync($"api/v1/accounts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
