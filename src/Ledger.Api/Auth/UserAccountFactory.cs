using Ledger.Api.Data;
using Ledger.Api.Domain;

namespace Ledger.Api.Auth;

/// <summary>
/// Shared initial data for accounts created either from configuration or self-registration.
/// </summary>
public static class UserAccountFactory
{
    public static void AddDefaultCategories(LedgerDbContext db, Guid userId)
    {
        var isOwner = userId == LedgerSeed.OwnerUserId;

        foreach (var seed in LedgerSeed.DefaultCategories)
        {
            db.Categories.Add(new Category
            {
                Id = isOwner ? seed.Id : Guid.NewGuid(),
                UserId = userId,
                Name = seed.Name,
                Kind = seed.Kind,
                IsArchived = false,
                CreatedAtUtc = LedgerSeed.DefaultCategoryCreatedAtUtc
            });
        }
    }
}
