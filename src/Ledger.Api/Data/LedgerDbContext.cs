using Ledger.Api.Auth;
using Ledger.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Api.Data;

public sealed class LedgerDbContext : DbContext
{
    private readonly ICurrentUser? _currentUser;

    public LedgerDbContext(
        DbContextOptions<LedgerDbContext> options,
        ICurrentUser? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Budget> Budgets => Set<Budget>();

    private Guid? CurrentUserId => _currentUser?.UserId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);

        modelBuilder.Entity<Account>()
            .HasQueryFilter(account =>
                CurrentUserId != null && account.UserId == CurrentUserId.Value);

        modelBuilder.Entity<Category>()
            .HasQueryFilter(category =>
                CurrentUserId != null && category.UserId == CurrentUserId.Value);

        modelBuilder.Entity<Transaction>()
            .HasQueryFilter(transaction =>
                CurrentUserId != null && transaction.UserId == CurrentUserId.Value);

        modelBuilder.Entity<Budget>()
            .HasQueryFilter(budget =>
                CurrentUserId != null && budget.UserId == CurrentUserId.Value);
    }
}
