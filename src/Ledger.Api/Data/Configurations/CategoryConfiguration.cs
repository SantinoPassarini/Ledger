using Ledger.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Api.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(category => category.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(category => category.IsArchived)
            .HasDefaultValue(false);

        builder.Property(category => category.CreatedAtUtc)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(category => new { category.Name, category.Kind })
            .IsUnique()
            .HasFilter("\"IsArchived\" = FALSE");

        builder.HasData(
            Seed("11111111-1111-1111-1111-111111111101", "Salary", CategoryKind.Income),
            Seed("11111111-1111-1111-1111-111111111102", "Freelance", CategoryKind.Income),
            Seed("11111111-1111-1111-1111-111111111103", "Investments", CategoryKind.Income),
            Seed("11111111-1111-1111-1111-111111111104", "Other Income", CategoryKind.Income),
            Seed("11111111-1111-1111-1111-111111111201", "Food", CategoryKind.Expense),
            Seed("11111111-1111-1111-1111-111111111202", "Groceries", CategoryKind.Expense),
            Seed("11111111-1111-1111-1111-111111111203", "Transport", CategoryKind.Expense),
            Seed("11111111-1111-1111-1111-111111111204", "Housing", CategoryKind.Expense),
            Seed("11111111-1111-1111-1111-111111111205", "Utilities", CategoryKind.Expense),
            Seed("11111111-1111-1111-1111-111111111206", "Health", CategoryKind.Expense),
            Seed("11111111-1111-1111-1111-111111111207", "Entertainment", CategoryKind.Expense),
            Seed("11111111-1111-1111-1111-111111111208", "Education", CategoryKind.Expense),
            Seed("11111111-1111-1111-1111-111111111209", "Clothing", CategoryKind.Expense),
            Seed("11111111-1111-1111-1111-111111111210", "Subscriptions", CategoryKind.Expense),
            Seed("11111111-1111-1111-1111-111111111211", "Other Expenses", CategoryKind.Expense));
    }

    private static Category Seed(string id, string name, CategoryKind kind)
    {
        return new Category
        {
            Id = Guid.Parse(id),
            Name = name,
            Kind = kind,
            IsArchived = false,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }
}
