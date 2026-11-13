using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ledger.Desktop.Models;

namespace Ledger.Desktop.Api;

public sealed class LedgerApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private string? _accessToken;
    private bool _sessionExpiredRaised;

    public LedgerApiClient(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("The API base URL is required.", nameof(baseUrl));
        }

        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            // Render Free spins down after idle and needs up to about a minute to wake.
            // Keep the client alive long enough for that first request to complete.
            Timeout = TimeSpan.FromSeconds(100)
        };
    }

    public event EventHandler? SessionExpired;

    public string BaseUrl => _http.BaseAddress?.ToString() ?? string.Empty;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);

    public UserModel? CurrentUser { get; private set; }

    public async Task<UserModel> LoginAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken = default)
    {
        return await LoginAsync(new LoginInput(identifier, password), cancellationToken);
    }

    public async Task<UserModel> LoginAsync(
        LoginInput input,
        CancellationToken cancellationToken = default)
    {
        _accessToken = null;
        CurrentUser = null;
        _sessionExpiredRaised = false;

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/login")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
        var result = await DeserializeAsync<LoginResultModel>(response, cancellationToken);

        _accessToken = result.AccessToken;
        CurrentUser = result.User;
        return result.User;
    }

    public async Task<UserModel> GetCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await GetAsync<UserModel>("api/v1/auth/me", cancellationToken);
        CurrentUser = user;
        return user;
    }

    public async Task<RegisterResultModel> RegisterAsync(
        RegisterInput input,
        CancellationToken cancellationToken = default)
    {
        _accessToken = null;
        CurrentUser = null;
        _sessionExpiredRaised = false;

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/register")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
        var result = await DeserializeAsync<RegisterResultModel>(response, cancellationToken);

        _accessToken = result.AccessToken;
        CurrentUser = result.User;
        return result;
    }

    public async Task ChangePasswordAsync(
        ChangePasswordInput input,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/change-password")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
    }

    public void Logout()
    {
        _accessToken = null;
        CurrentUser = null;
        _sessionExpiredRaised = false;
    }

    public async Task<IReadOnlyList<AccountModel>> GetAccountsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var path = AddQuery(
            "api/v1/accounts",
            ("includeArchived", includeArchived ? "true" : null));

        return await GetAsync<List<AccountModel>>(path, cancellationToken);
    }

    public async Task<AccountModel> CreateAccountAsync(
        AccountInput input,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/accounts")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
        return await DeserializeAsync<AccountModel>(response, cancellationToken);
    }

    public async Task UpdateAccountAsync(
        Guid id,
        AccountInput input,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/accounts/{id}")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task ArchiveAccountAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/accounts/{id}");
        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryModel>> GetCategoriesAsync(
        CategoryKind? kind = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var path = AddQuery(
            "api/v1/categories",
            ("kind", kind?.ToString()),
            ("includeArchived", includeArchived ? "true" : null));

        return await GetAsync<List<CategoryModel>>(path, cancellationToken);
    }

    public async Task<CategoryModel> CreateCategoryAsync(
        CategoryInput input,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/categories")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
        return await DeserializeAsync<CategoryModel>(response, cancellationToken);
    }

    public async Task UpdateCategoryAsync(
        Guid id,
        CategoryInput input,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/categories/{id}")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task ArchiveCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/categories/{id}");
        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task<PagedResponse<TransactionModel>> GetTransactionsAsync(
        TransactionQuery query,
        CancellationToken cancellationToken = default)
    {
        var path = AddQuery(
            "api/v1/transactions",
            ("from", query.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("to", query.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("accountId", query.AccountId?.ToString()),
            ("categoryId", query.CategoryId?.ToString()),
            ("type", query.Type?.ToString()),
            ("search", string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim()),
            ("includeArchived", query.IncludeArchived ? "true" : null),
            ("page", query.Page.ToString(CultureInfo.InvariantCulture)),
            ("pageSize", query.PageSize.ToString(CultureInfo.InvariantCulture)));

        return await GetAsync<PagedResponse<TransactionModel>>(path, cancellationToken);
    }

    public async Task<TransactionModel> CreateTransactionAsync(
        TransactionInput input,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/transactions")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
        return await DeserializeAsync<TransactionModel>(response, cancellationToken);
    }

    public async Task UpdateTransactionAsync(
        Guid id,
        TransactionInput input,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/transactions/{id}")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task ArchiveTransactionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/transactions/{id}");
        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task<TransferModel> CreateTransferAsync(
        TransferInput input,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/transfers")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
        return await DeserializeAsync<TransferModel>(response, cancellationToken);
    }

    public async Task<TransferModel> GetTransferAsync(
        Guid transferGroupId,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<TransferModel>(
            $"api/v1/transfers/{transferGroupId}",
            cancellationToken);
    }

    public async Task ArchiveTransferAsync(
        Guid transferGroupId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/transfers/{transferGroupId}");
        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task<DashboardModel> GetDashboardAsync(
        DateOnly? month = null,
        CancellationToken cancellationToken = default)
    {
        var path = AddQuery(
            "api/v1/dashboard",
            ("month", month?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

        return await GetAsync<DashboardModel>(path, cancellationToken);
    }

    public async Task<IReadOnlyList<BudgetModel>> GetBudgetsAsync(
        DateOnly? month = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var path = AddQuery(
            "api/v1/budgets",
            ("month", month?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("includeArchived", includeArchived ? "true" : null));

        return await GetAsync<List<BudgetModel>>(path, cancellationToken);
    }

    public async Task<BudgetModel> CreateBudgetAsync(
        BudgetInput input,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/budgets")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
        return await DeserializeAsync<BudgetModel>(response, cancellationToken);
    }

    public async Task<BudgetModel> UpdateBudgetAsync(
        Guid id,
        BudgetUpdateInput input,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/budgets/{id}")
        {
            Content = JsonContent.Create(input, options: JsonOptions)
        };

        using var response = await SendAsync(request, cancellationToken);
        return await DeserializeAsync<BudgetModel>(response, cancellationToken);
    }

    public async Task ArchiveBudgetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/budgets/{id}");
        using var response = await SendAsync(request, cancellationToken);
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendAsync(request, cancellationToken);

        return await DeserializeAsync<T>(response, cancellationToken);
    }

    private static async Task<T> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);

        return result ?? throw new ApiException(
            (int)response.StatusCode,
            "The API returned an empty response.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            IsAuthenticated &&
            !_sessionExpiredRaised)
        {
            _sessionExpiredRaised = true;
            SessionExpired?.Invoke(this, EventArgs.Empty);
        }

        var statusCode = (int)response.StatusCode;
        var message = await ReadErrorAsync(response, cancellationToken);
        response.Dispose();

        throw new ApiException(statusCode, message);
    }

    private static async Task<string> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                var messages = new List<string>();

                if (root.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in errors.EnumerateObject())
                    {
                        if (property.Value.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var item in property.Value.EnumerateArray())
                        {
                            var text = item.GetString();

                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                messages.Add(text);
                            }
                        }
                    }
                }

                AddMessage(messages, root, "detail");
                AddMessage(messages, root, "title");

                if (messages.Count > 0)
                {
                    return string.Join(Environment.NewLine, messages.Distinct());
                }
            }
            catch (JsonException)
            {
            }

            return body;
        }

        return $"The API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
    }

    private static void AddMessage(List<string> messages, JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();

            if (!string.IsNullOrWhiteSpace(text))
            {
                messages.Add(text);
            }
        }
    }

    private static string AddQuery(string path, params (string Name, string? Value)[] parameters)
    {
        var parts = new List<string>();

        foreach (var (name, value) in parameters)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{name}={Uri.EscapeDataString(value)}");
            }
        }

        return parts.Count == 0 ? path : $"{path}?{string.Join("&", parts)}";
    }
}
