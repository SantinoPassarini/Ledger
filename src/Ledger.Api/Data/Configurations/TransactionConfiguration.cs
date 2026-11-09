using Ledger.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Api.Data.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions", table =>
        {
            table.HasCheckConstraint("CK_Transactions_Amount_Positive", "\"Amount\" > 0");
            table.HasCheckConstraint(
                "CK_Transactions_Category_Transfer",
                "(\"Type\" IN ('Income', 'Expense') AND \"CategoryId\" IS NOT NULL AND \"TransferGroupId\" IS NULL) OR " +
                "(\"Type\" IN ('TransferOut', 'TransferIn') AND \"CategoryId\" IS NULL AND \"TransferGroupId\" IS NOT NULL)");
        });

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.UserId)
            .IsRequired();

        builder.HasOne(transaction => transaction.User)
            .WithMany(user => user.Transactions)
            .HasForeignKey(transaction => transaction.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(transaction => transaction.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(transaction => transaction.Amount)
            .HasColumnType("numeric(18,2)");

        builder.Property(transaction => transaction.Date)
            .HasColumnType("date");

        builder.Property(transaction => transaction.Description)
            .HasMaxLength(200);

        builder.Property(transaction => transaction.IsArchived)
            .HasDefaultValue(false);

        builder.Property(transaction => transaction.CreatedAtUtc)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(transaction => transaction.UpdatedAtUtc)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(transaction => transaction.Account)
            .WithMany(account => account.Transactions)
            .HasForeignKey(transaction => transaction.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transaction => transaction.Category)
            .WithMany(category => category.Transactions)
            .HasForeignKey(transaction => transaction.CategoryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(transaction => new { transaction.UserId, transaction.Date });
        builder.HasIndex(transaction => new { transaction.AccountId, transaction.Date });
        builder.HasIndex(transaction => new { transaction.CategoryId, transaction.Date });
        builder.HasIndex(transaction => transaction.Date);
        builder.HasIndex(transaction => transaction.TransferGroupId);
        builder.HasIndex(transaction => new { transaction.TransferGroupId, transaction.Type })
            .IsUnique()
            .HasFilter("\"TransferGroupId\" IS NOT NULL");
    }
}
