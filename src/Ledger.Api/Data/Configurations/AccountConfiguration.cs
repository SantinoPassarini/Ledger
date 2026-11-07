using Ledger.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Api.Data.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(account => account.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(account => account.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("ARS");

        builder.Property(account => account.IsArchived)
            .HasDefaultValue(false);

        builder.Property(account => account.CreatedAtUtc)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");
    }
}
