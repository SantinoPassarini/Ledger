using Ledger.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(user => user.NormalizedUsername)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(user => user.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(user => user.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(user => user.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(user => user.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(user => user.IsActive)
            .HasDefaultValue(true);

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(user => user.NormalizedUsername)
            .IsUnique();

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique();
    }
}
