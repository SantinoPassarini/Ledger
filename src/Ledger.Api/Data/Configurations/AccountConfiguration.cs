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

        builder.Property(account => account.UserId)
            .IsRequired();

        builder.HasOne(account => account.User)
            .WithMany(user => user.Accounts)
            .HasForeignKey(account => account.UserId)
            .OnDelete(DeleteBehavior.Restrict);

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

        builder.HasIndex(account => new { account.UserId, account.Name });
    }
}
