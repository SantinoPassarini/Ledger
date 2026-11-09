using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ledger.Api.Contracts.Accounts;
using Ledger.Api.Contracts.Auth;
using Ledger.Api.Contracts.Categories;
using Ledger.Api.Data;
using Ledger.Api.Domain;

namespace Ledger.Api.Tests;

[Collection("api")]
public sealed class RegistrationTests : ApiTestBase
{
    private const string Password = "Registration123!";

    public RegistrationTests(LedgerApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Register_WithEmail_CreatesAccountSessionAndDefaultCategories()
    {
        var username = NewUsername();
        var email = $"{username}@example.test";
        using var client = Factory.CreateUnauthenticatedClient();

        using var response = await RegisterAsync(
            client,
            new RegisterRequest
            {
                Username = username,
                Email = email,
                Password = Password,
                InviteCode = LedgerApiFactory.RegistrationInviteCode,
                DisplayName = "Registered User"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await ReadAsync<RegisterResponse>(response);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.True(body.ExpiresAtUtc > DateTime.UtcNow);
        Assert.False(body.EmailGenerated);
        Assert.Equal(username, body.User.Username);
        Assert.Equal(email, body.User.Email);
        Assert.Equal("Registered User", body.User.DisplayName);

        using var loginClient = Factory.CreateUnauthenticatedClient();
        var token = await Factory.LoginAsync(loginClient, username, Password);
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.AccessToken);

        using var categoriesResponse = await client.GetAsync(
            "api/v1/categories?includeArchived=true");
        Assert.Equal(HttpStatusCode.OK, categoriesResponse.StatusCode);

        var categories = await ReadAsync<List<CategoryResponse>>(categoriesResponse);
        Assert.True(categories.Count >= LedgerSeed.DefaultCategories.Count);
        Assert.Contains(categories, category => category.Name == "Salary");
        Assert.Contains(categories, category => category.Name == "Food");
    }

    [Fact]
    public async Task Register_WithoutEmail_GeneratesLocalAddressAndReportsIt()
    {
        var username = NewUsername();
        using var client = Factory.CreateUnauthenticatedClient();

        using var response = await RegisterAsync(
            client,
            new RegisterRequest
            {
                Username = username,
                Email = null,
                Password = Password,
                InviteCode = LedgerApiFactory.RegistrationInviteCode
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await ReadAsync<RegisterResponse>(response);
        Assert.True(body.EmailGenerated);
        Assert.Equal($"{username}@ledger.local", body.User.Email);

        using var loginClient = Factory.CreateUnauthenticatedClient();
        var token = await Factory.LoginAsync(loginClient, body.User.Email, Password);
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ReturnsConflict()
    {
        var username = NewUsername();
        using var client = Factory.CreateUnauthenticatedClient();

        using var first = await RegisterAsync(
            client,
            ValidRequest(username, $"{username}@example.test"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var duplicate = await RegisterAsync(
            client,
            ValidRequest(username.ToUpperInvariant(), $"other-{username}@example.test"));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains(
            "username",
            (await ReadErrorAsync(duplicate)).ToLowerInvariant());
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var username = NewUsername();
        var email = $"{username}@example.test";
        using var client = Factory.CreateUnauthenticatedClient();

        using var first = await RegisterAsync(client, ValidRequest(username, email));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var duplicate = await RegisterAsync(
            client,
            ValidRequest(NewUsername(), email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains(
            "email",
            (await ReadErrorAsync(duplicate)).ToLowerInvariant());
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        using var client = Factory.CreateUnauthenticatedClient();

        using var response = await RegisterAsync(
            client,
            ValidRequest(NewUsername(), "not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        using var client = Factory.CreateUnauthenticatedClient();
        var username = NewUsername();

        using var response = await RegisterAsync(
            client,
            new RegisterRequest
            {
                Username = username,
                Email = $"{username}@example.test",
                Password = "short",
                InviteCode = LedgerApiFactory.RegistrationInviteCode
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidInviteCode_ReturnsBadRequest()
    {
        using var client = Factory.CreateUnauthenticatedClient();
        var username = NewUsername();

        using var response = await RegisterAsync(
            client,
            new RegisterRequest
            {
                Username = username,
                Email = $"{username}@example.test",
                Password = Password,
                InviteCode = "wrong-invite-code"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisteredUser_DataIsIsolatedFromOtherUsers()
    {
        var username = NewUsername();
        using var registeredClient = Factory.CreateUnauthenticatedClient();

        using var registerResponse = await RegisterAsync(
            registeredClient,
            ValidRequest(username, $"{username}@example.test"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var session = await ReadAsync<RegisterResponse>(registerResponse);
        registeredClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var accountName = Unique("Registered user account");
        using var createResponse = await registeredClient.PostAsJsonAsync(
            "api/v1/accounts",
            new CreateAccountRequest { Name = accountName, Type = AccountType.Wallet });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var account = await ReadAsync<AccountResponse>(createResponse);

        using var ownerAccountsResponse = await Client.GetAsync("api/v1/accounts");
        Assert.Equal(HttpStatusCode.OK, ownerAccountsResponse.StatusCode);
        var ownerAccounts = await ReadAsync<List<AccountResponse>>(ownerAccountsResponse);
        Assert.DoesNotContain(ownerAccounts, candidate => candidate.Id == account.Id);

        using var crossAccess = await Client.GetAsync($"api/v1/accounts/{account.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossAccess.StatusCode);
    }

    private static RegisterRequest ValidRequest(string username, string? email)
    {
        return new RegisterRequest
        {
            Username = username,
            Email = email,
            Password = Password,
            InviteCode = LedgerApiFactory.RegistrationInviteCode
        };
    }

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        RegisterRequest request)
    {
        return client.PostAsJsonAsync("api/v1/auth/register", request);
    }

    private static string NewUsername()
    {
        return $"reg-{Guid.NewGuid():N}";
    }
}
