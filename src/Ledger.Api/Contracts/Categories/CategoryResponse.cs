using Ledger.Api.Domain;

namespace Ledger.Api.Contracts.Categories;

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    CategoryKind Kind,
    bool IsArchived,
    DateTime CreatedAtUtc);
