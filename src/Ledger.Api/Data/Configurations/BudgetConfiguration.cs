using Ledger.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Api.Data.Configurations;

public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets", table =>
            table.HasCheckConstraint("CK_Budgets_Amount_Positive", "\"Amount\" > 0"));

        builder.HasKey(budget => budget.Id);

        builder.Property(budget => budget.UserId)
            .IsRequired();

        builder.HasOne(budget => budget.User)
            .WithMany(user => user.Budgets)
            .HasForeignKey(budget => budget.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(budget => budget.Month)
            .HasColumnType("date");

        builder.Property(budget => budget.Amount)
            .HasColumnType("numeric(18,2)");

        builder.Property(budget => budget.IsArchived)
            .HasDefaultValue(false);

        builder.Property(budget => budget.CreatedAtUtc)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(budget => budget.UpdatedAtUtc)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(budget => budget.Category)
            .WithMany(category => category.Budgets)
            .HasForeignKey(budget => budget.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(budget => new { budget.UserId, budget.CategoryId, budget.Month })
            .IsUnique();

        builder.HasIndex(budget => budget.Month);
    }
}
