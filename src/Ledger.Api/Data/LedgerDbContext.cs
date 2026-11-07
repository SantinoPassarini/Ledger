using Ledger.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Api.Data;

public sealed class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);
    }
}
