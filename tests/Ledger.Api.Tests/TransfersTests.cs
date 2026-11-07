using System.Net;
using Ledger.Api.Contracts.Common;
using Ledger.Api.Contracts.Transactions;
using Ledger.Api.Contracts.Transfers;
using Ledger.Api.Domain;

namespace Ledger.Api.Tests;

[Collection("api")]
public sealed class TransfersTests : ApiTestBase
{
    public TransfersTests(LedgerApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateTransfer_CreatesTwoLegsAndMovesBalance()
    {
        var source = await CreateAccountAsync();
        var destination = await CreateAccountAsync();
        var incomeCategory = await CreateCategoryAsync(kind: CategoryKind.Income);
        await CreateTransactionAsync(
            source.Id,
            incomeCategory.Id,
            TransactionType.Income,
            1000m);

        var description = Unique("Transfer description");
        var transfer = await CreateTransferAsync(
            source.Id,
            destination.Id,
            300m,
            description: description);

        Assert.Equal(TransactionType.TransferOut, transfer.OutTransaction.Type);
        Assert.Equal(TransactionType.TransferIn, transfer.InTransaction.Type);
        Assert.Null(transfer.OutTransaction.CategoryId);
        Assert.Null(transfer.OutTransaction.CategoryName);
        Assert.Null(transfer.InTransaction.CategoryId);
        Assert.Null(transfer.InTransaction.CategoryName);
        Assert.Equal(transfer.TransferGroupId, transfer.OutTransaction.TransferGroupId);
        Assert.Equal(transfer.TransferGroupId, transfer.InTransaction.TransferGroupId);
        Assert.Equal(300m, transfer.OutTransaction.Amount);
        Assert.Equal(300m, transfer.InTransaction.Amount);
        Assert.Equal(description, transfer.OutTransaction.Description);
        Assert.Equal(description, transfer.InTransaction.Description);

        Assert.Equal(700m, (await GetAccountAsync(source.Id)).Balance);
        Assert.Equal(300m, (await GetAccountAsync(destination.Id)).Balance);
    }

    [Fact]
    public async Task GetTransferByGroupId_ReturnsBothLegs()
    {
        var source = await CreateAccountAsync();
        var destination = await CreateAccountAsync();
        var transfer = await CreateTransferAsync(source.Id, destination.Id, 25m);

        using var response = await Client.GetAsync(
            $"api/v1/transfers/{transfer.TransferGroupId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync<TransferResponse>(response);
        Assert.Equal(transfer.TransferGroupId, body.TransferGroupId);
        Assert.Equal(source.Id, body.OutTransaction.AccountId);
        Assert.Equal(destination.Id, body.InTransaction.AccountId);
    }

    [Fact]
    public async Task ArchiveTransfer_ArchivesBothLegs()
    {
        var source = await CreateAccountAsync();
        var destination = await CreateAccountAsync();
        var transfer = await CreateTransferAsync(source.Id, destination.Id, 25m);

        using var response = await Client.DeleteAsync(
            $"api/v1/transfers/{transfer.TransferGroupId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var visibleResponse = await Client.GetAsync(
            $"api/v1/transactions?accountId={source.Id}");
        var visible = await ReadAsync<PagedResponse<TransactionResponse>>(visibleResponse);
        Assert.Equal(0, visible.TotalCount);

        using var allResponse = await Client.GetAsync(
            $"api/v1/transactions?accountId={source.Id}&includeArchived=true");
        var all = await ReadAsync<PagedResponse<TransactionResponse>>(allResponse);
        Assert.Single(all.Items);
        Assert.True(all.Items[0].IsArchived);

        Assert.Equal(0m, (await GetAccountAsync(source.Id)).Balance);
        Assert.Equal(0m, (await GetAccountAsync(destination.Id)).Balance);
    }

    [Fact]
    public async Task ArchivingOneLegThroughTransactionsEndpoint_ArchivesTheGroup()
    {
        var source = await CreateAccountAsync();
        var destination = await CreateAccountAsync();
        var transfer = await CreateTransferAsync(source.Id, destination.Id, 25m);

        using var response = await Client.DeleteAsync(
            $"api/v1/transactions/{transfer.OutTransaction.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var allResponse = await Client.GetAsync(
            $"api/v1/transactions?accountId={destination.Id}&includeArchived=true");
        var all = await ReadAsync<PagedResponse<TransactionResponse>>(allResponse);
        Assert.Single(all.Items);
        Assert.True(all.Items[0].IsArchived);
    }

    [Fact]
    public async Task UpdateTransfer_ReturnsConflict()
    {
        var source = await CreateAccountAsync();
        var destination = await CreateAccountAsync();
        var transfer = await CreateTransferAsync(source.Id, destination.Id, 25m);

        using var response = await PutJsonAsync(
            $"api/v1/transactions/{transfer.OutTransaction.Id}",
            new UpdateTransactionRequest
            {
                AccountId = source.Id,
                CategoryId = Guid.NewGuid(),
                Type = TransactionType.TransferOut,
                Amount = 50m,
                Date = transfer.OutTransaction.Date
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TransferToSameAccount_ReturnsValidationProblem()
    {
        var account = await CreateAccountAsync();

        using var response = await PostJsonAsync(
            "api/v1/transfers",
            new CreateTransferRequest
            {
                FromAccountId = account.Id,
                ToAccountId = account.Id,
                Amount = 10m,
                Date = DateOnly.FromDateTime(DateTime.Today)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TransferFromArchivedAccount_ReturnsConflict()
    {
        var source = await CreateAccountAsync();
        var destination = await CreateAccountAsync();

        using var archiveResponse = await Client.DeleteAsync($"api/v1/accounts/{source.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        using var response = await PostJsonAsync(
            "api/v1/transfers",
            new CreateTransferRequest
            {
                FromAccountId = source.Id,
                ToAccountId = destination.Id,
                Amount = 10m,
                Date = DateOnly.FromDateTime(DateTime.Today)
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task InvalidTransferAmount_ReturnsValidationProblem()
    {
        var source = await CreateAccountAsync();
        var destination = await CreateAccountAsync();

        using var response = await PostJsonAsync(
            "api/v1/transfers",
            new CreateTransferRequest
            {
                FromAccountId = source.Id,
                ToAccountId = destination.Id,
                Amount = 0m,
                Date = DateOnly.FromDateTime(DateTime.Today)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TransferLegs_CanBeFilteredByType()
    {
        var source = await CreateAccountAsync();
        var destination = await CreateAccountAsync();
        await CreateTransferAsync(source.Id, destination.Id, 10m);

        using var response = await Client.GetAsync(
            $"api/v1/transactions?accountId={source.Id}&type=TransferOut");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResponse<TransactionResponse>>(response);
        Assert.Single(page.Items);
        Assert.Equal(TransactionType.TransferOut, page.Items[0].Type);
    }
}
