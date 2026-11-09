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

        builder.Property(category => category.UserId)
            .IsRequired();

        builder.HasOne(category => category.User)
            .WithMany(user => user.Categories)
            .HasForeignKey(category => category.UserId)
            .OnDelete(DeleteBehavior.Restrict);

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

        builder.HasIndex(category => new { category.UserId, category.Name, category.Kind })
            .IsUnique()
            .HasFilter("\"IsArchived\" = FALSE");

        builder.HasData(LedgerSeed.DefaultCategories
            .Select(category => Seed(category.Id, category.Name, category.Kind))
            .ToArray());
    }

    private static Category Seed(Guid id, string name, CategoryKind kind)
    {
        return new Category
        {
            Id = id,
            UserId = LedgerSeed.OwnerUserId,
            Name = name,
            Kind = kind,
            IsArchived = false,
            CreatedAtUtc = LedgerSeed.DefaultCategoryCreatedAtUtc
        };
    }
}
