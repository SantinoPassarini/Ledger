using System.Net;
using Ledger.Api.Contracts.Categories;
using Ledger.Api.Domain;

namespace Ledger.Api.Tests;

[Collection("api")]
public sealed class CategoriesTests : ApiTestBase
{
    public CategoriesTests(LedgerApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task SeedCategories_ArePresent()
    {
        using var response = await Client.GetAsync("api/v1/categories?includeArchived=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await ReadAsync<List<CategoryResponse>>(response);
        Assert.True(categories.Count >= 15);
    }

    [Fact]
    public async Task CreateCategory_ReturnsCreatedCategory()
    {
        var category = await CreateCategoryAsync();

        using var response = await Client.GetAsync($"api/v1/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync<CategoryResponse>(response);
        Assert.Equal(category.Id, body.Id);
        Assert.Equal(category.Name, body.Name);
        Assert.Equal(CategoryKind.Expense, body.Kind);
    }

    [Fact]
    public async Task DuplicateActiveCategory_ReturnsConflict()
    {
        var category = await CreateCategoryAsync();

        using var response = await PostJsonAsync(
            "api/v1/categories",
            new CreateCategoryRequest { Name = category.Name, Kind = category.Kind });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_ChangesName()
    {
        var category = await CreateCategoryAsync();
        var newName = Unique("Updated category");

        using var response = await PutJsonAsync(
            $"api/v1/categories/{category.Id}",
            new UpdateCategoryRequest { Name = newName, Kind = category.Kind });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var getResponse = await Client.GetAsync($"api/v1/categories/{category.Id}");
        var updated = await ReadAsync<CategoryResponse>(getResponse);
        Assert.Equal(newName, updated.Name);
    }

    [Fact]
    public async Task ArchivedCategory_IsHiddenByDefaultAndVisibleWithIncludeArchived()
    {
        var category = await CreateCategoryAsync();

        using var archiveResponse = await Client.DeleteAsync($"api/v1/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        using var visibleResponse = await Client.GetAsync("api/v1/categories");
        var visible = await ReadAsync<List<CategoryResponse>>(visibleResponse);
        Assert.DoesNotContain(visible, item => item.Id == category.Id);

        using var allResponse = await Client.GetAsync("api/v1/categories?includeArchived=true");
        var all = await ReadAsync<List<CategoryResponse>>(allResponse);
        Assert.Contains(all, item => item.Id == category.Id && item.IsArchived);
    }

    [Fact]
    public async Task ChangingKindOfUsedCategory_ReturnsConflict()
    {
        var account = await CreateAccountAsync();
        var category = await CreateCategoryAsync(kind: CategoryKind.Income);
        await CreateTransactionAsync(account.Id, category.Id, TransactionType.Income, 10m);

        using var response = await PutJsonAsync(
            $"api/v1/categories/{category.Id}",
            new UpdateCategoryRequest { Name = category.Name, Kind = CategoryKind.Expense });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
