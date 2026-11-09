using Ledger.Api.Domain;

namespace Ledger.Api.Data;

public static class LedgerSeed
{
    public static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111001");

    public static IReadOnlyList<DefaultCategory> DefaultCategories { get; } =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111101"), "Salary", CategoryKind.Income),
        new(Guid.Parse("11111111-1111-1111-1111-111111111102"), "Freelance", CategoryKind.Income),
        new(Guid.Parse("11111111-1111-1111-1111-111111111103"), "Investments", CategoryKind.Income),
        new(Guid.Parse("11111111-1111-1111-1111-111111111104"), "Other Income", CategoryKind.Income),
        new(Guid.Parse("11111111-1111-1111-1111-111111111201"), "Food", CategoryKind.Expense),
        new(Guid.Parse("11111111-1111-1111-1111-111111111202"), "Groceries", CategoryKind.Expense),
        new(Guid.Parse("11111111-1111-1111-1111-111111111203"), "Transport", CategoryKind.Expense),
        new(Guid.Parse("11111111-1111-1111-1111-111111111204"), "Housing", CategoryKind.Expense),
        new(Guid.Parse("11111111-1111-1111-1111-111111111205"), "Utilities", CategoryKind.Expense),
        new(Guid.Parse("11111111-1111-1111-1111-111111111206"), "Health", CategoryKind.Expense),
        new(Guid.Parse("11111111-1111-1111-1111-111111111207"), "Entertainment", CategoryKind.Expense),
        new(Guid.Parse("11111111-1111-1111-1111-111111111208"), "Education", CategoryKind.Expense),
        new(Guid.Parse("11111111-1111-1111-1111-111111111209"), "Clothing", CategoryKind.Expense),
        new(Guid.Parse("11111111-1111-1111-1111-111111111210"), "Subscriptions", CategoryKind.Expense),
        new(Guid.Parse("11111111-1111-1111-1111-111111111211"), "Other Expenses", CategoryKind.Expense)
    ];

    public static readonly DateTime DefaultCategoryCreatedAtUtc =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}

public sealed record DefaultCategory(Guid Id, string Name, CategoryKind Kind);
