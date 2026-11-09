using Ledger.Api.Auth;
using Ledger.Api.Contracts.Categories;
using Ledger.Api.Data;
using Ledger.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ledger.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly LedgerDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CategoriesController(LedgerDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(
        [FromQuery] CategoryKind? kind,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(category => !category.IsArchived);
        }

        if (kind.HasValue)
        {
            query = query.Where(category => category.Kind == kind.Value);
        }

        var categories = await query
            .OrderBy(category => category.Kind)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);

        return Ok(categories.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return category is null ? NotFound() : Ok(ToResponse(category));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
            return ValidationProblem(ModelState);
        }

        var kind = request.Kind!.Value;

        var duplicateExists = await _db.Categories.AnyAsync(
            category => !category.IsArchived &&
                        category.Name == name &&
                        category.Kind == kind,
            cancellationToken);

        if (duplicateExists)
        {
            return DuplicateCategoryProblem();
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.RequireUserId(),
            Name = name,
            Kind = kind,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Categories.Add(category);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return DuplicateCategoryProblem();
        }

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, ToResponse(category));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        if (category.IsArchived)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Category is archived",
                detail: "Archived categories cannot be modified.");
        }

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
            return ValidationProblem(ModelState);
        }

        var kind = request.Kind!.Value;

        if (kind != category.Kind)
        {
            var hasTransactions = await _db.Transactions.AnyAsync(
                transaction => transaction.CategoryId == id,
                cancellationToken);

            if (hasTransactions)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Category kind cannot be changed",
                    detail: "The category has transactions. Create a new category instead.");
            }
        }

        var duplicateExists = await _db.Categories.AnyAsync(
            candidate => candidate.Id != id &&
                         !candidate.IsArchived &&
                         candidate.Name == name &&
                         candidate.Kind == kind,
            cancellationToken);

        if (duplicateExists)
        {
            return DuplicateCategoryProblem();
        }

        category.Name = name;
        category.Kind = kind;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return DuplicateCategoryProblem();
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        if (!category.IsArchived)
        {
            category.IsArchived = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
    }

    private ObjectResult DuplicateCategoryProblem()
    {
        return Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Category already exists",
            detail: "An active category with the same name and kind already exists.");
    }

    private static CategoryResponse ToResponse(Category category)
    {
        return new CategoryResponse(
            category.Id,
            category.Name,
            category.Kind,
            category.IsArchived,
            category.CreatedAtUtc);
    }
}
