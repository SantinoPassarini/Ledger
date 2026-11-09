using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ledger.Api.Contracts.Accounts;
using Ledger.Api.Contracts.Auth;
using Ledger.Api.Contracts.Categories;
using Ledger.Api.Domain;

namespace Ledger.Api.Tests;

[Collection("api")]
public sealed class AuthTests : ApiTestBase
{
    public AuthTests(LedgerApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Login_WithUsername_ReturnsTokenAndUser()
    {
        using var response = await Factory.CreateUnauthenticatedClient()
            .PostAsJsonAsync(
                "api/v1/auth/login",
                new LoginRequest
                {
                    Identifier = Factory.OwnerUsername,
                    Password = Factory.OwnerPassword
                });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<LoginResponse>(response);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.True(body.ExpiresAtUtc > DateTime.UtcNow);
        Assert.Equal(Factory.OwnerUsername, body.User.Username);
        Assert.Equal(Factory.OwnerEmail, body.User.Email);
        Assert.NotEqual(Guid.Empty, body.User.Id);
    }

    [Fact]
    public async Task Login_WithEmail_ReturnsTokenAndUser()
    {
        using var response = await Factory.CreateUnauthenticatedClient()
            .PostAsJsonAsync(
                "api/v1/auth/login",
                new LoginRequest
                {
                    Identifier = Factory.OwnerEmail,
                    Password = Factory.OwnerPassword
                });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<LoginResponse>(response);
        Assert.Equal(Factory.OwnerUsername, body.User.Username);
        Assert.Equal(Factory.OwnerEmail, body.User.Email);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        using var response = await Factory.CreateUnauthenticatedClient()
            .PostAsJsonAsync(
                "api/v1/auth/login",
                new LoginRequest
                {
                    Identifier = Factory.OwnerUsername,
                    Password = "definitely-not-the-password"
                });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConfiguredAdditionalUser_IsProvisionedAndCanSignIn()
    {
        using var response = await Factory.CreateUnauthenticatedClient()
            .PostAsJsonAsync(
                "api/v1/auth/login",
                new LoginRequest
                {
                    Identifier = Factory.SecondUsername,
                    Password = Factory.SecondPassword
                });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<LoginResponse>(response);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal(Factory.SecondUsername, body.User.Username);
        Assert.Equal(Factory.SecondEmail, body.User.Email);
        Assert.NotEqual(Guid.Empty, body.User.Id);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        using var client = Factory.CreateUnauthenticatedClient();
        using var response = await client.GetAsync("api/v1/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_ReturnsUnauthorized()
    {
        using var client = Factory.CreateUnauthenticatedClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        using var response = await client.GetAsync("api/v1/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsTheAuthenticatedUser()
    {
        using var response = await Client.GetAsync("api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<UserResponse>(response);
        Assert.Equal(Factory.OwnerUsername, body.Username);
        Assert.Equal(Factory.OwnerEmail, body.Email);
        Assert.False(string.IsNullOrWhiteSpace(body.DisplayName));
    }

    [Fact]
    public async Task ChangePassword_UpdatesCredentialsAndReportsBadCurrentPassword()
    {
        const string newPassword = "LedgerChanged123!";

        try
        {
            using var invalid = await PostJsonAsync(
                "api/v1/auth/change-password",
                new ChangePasswordRequest
                {
                    CurrentPassword = "wrong-current-password",
                    NewPassword = newPassword
                });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

            using var changed = await PostJsonAsync(
                "api/v1/auth/change-password",
                new ChangePasswordRequest
                {
                    CurrentPassword = Factory.OwnerPassword,
                    NewPassword = newPassword
                });
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

            using var login = await Factory.CreateUnauthenticatedClient()
                .PostAsJsonAsync(
                    "api/v1/auth/login",
                    new LoginRequest
                    {
                        Identifier = Factory.OwnerUsername,
                        Password = newPassword
                    });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
        finally
        {
            using var restore = await PostJsonAsync(
                "api/v1/auth/change-password",
                new ChangePasswordRequest
                {
                    CurrentPassword = newPassword,
                    NewPassword = Factory.OwnerPassword
                });
            Assert.True(
                restore.IsSuccessStatusCode,
                "The test password could not be restored.");
        }
    }

    [Fact]
    public async Task ChangePassword_RejectsShortAndUnchangedPasswords()
    {
        using var shortPassword = await PostJsonAsync(
            "api/v1/auth/change-password",
            new ChangePasswordRequest
            {
                CurrentPassword = Factory.OwnerPassword,
                NewPassword = "short"
            });
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);

        using var unchanged = await PostJsonAsync(
            "api/v1/auth/change-password",
            new ChangePasswordRequest
            {
                CurrentPassword = Factory.OwnerPassword,
                NewPassword = Factory.OwnerPassword
            });
        Assert.Equal(HttpStatusCode.BadRequest, unchanged.StatusCode);
    }

    [Fact]
    public async Task Data_IsIsolatedBetweenUsers()
    {
        using var otherClient = Factory.CreateUnauthenticatedClient();
        var token = await Factory.LoginAsync(
            otherClient,
            Factory.SecondUsername,
            Factory.SecondPassword);
        otherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var otherAccountName = Unique("Other user account");
        using var createResponse = await otherClient.PostAsJsonAsync(
            "api/v1/accounts",
            new CreateAccountRequest { Name = otherAccountName, Type = AccountType.Wallet });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var otherAccount = await ReadAsync<AccountResponse>(createResponse);

        using var ownerAccounts = await Client.GetAsync("api/v1/accounts");
        Assert.Equal(HttpStatusCode.OK, ownerAccounts.StatusCode);
        var owners = await ReadAsync<List<AccountResponse>>(ownerAccounts);
        Assert.DoesNotContain(owners, account => account.Id == otherAccount.Id);

        using var crossAccess = await Client.GetAsync($"api/v1/accounts/{otherAccount.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossAccess.StatusCode);

        using var ownerCategories = await Client.GetAsync("api/v1/categories?includeArchived=true");
        var ownerCategoryList = await ReadAsync<List<CategoryResponse>>(ownerCategories);
        Assert.True(ownerCategoryList.Count >= 15);

        using var otherCategories = await otherClient.GetAsync(
            "api/v1/categories?includeArchived=true");
        var otherCategoryList = await ReadAsync<List<CategoryResponse>>(otherCategories);
        Assert.All(
            otherCategoryList,
            category => Assert.DoesNotContain(
                ownerCategoryList,
                ownerCategory => ownerCategory.Id == category.Id));
    }
}
