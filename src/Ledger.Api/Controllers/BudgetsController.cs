using Ledger.Api.Auth;
using Ledger.Api.Contracts.Budgets;
using Ledger.Api.Data;
using Ledger.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Api.Controllers;

[ApiController]
[Route("api/v1/budgets")]
public sealed class BudgetsController : ControllerBase
{
    private const decimal MaxAmount = 9_999_999_999_999_999.99m;

    private readonly LedgerDbContext _db;
    private readonly ICurrentUser _currentUser;

    public BudgetsController(LedgerDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetResponse>>> GetAll(
        [FromQuery] DateOnly? month,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var selectedMonth = NormalizeMonth(month ?? DateOnly.FromDateTime(DateTime.UtcNow));

        var budgets = await _db.Budgets
            .AsNoTracking()
            .Include(budget => budget.Category)
            .Where(budget =>
                budget.Month == selectedMonth &&
                (includeArchived || !budget.IsArchived))
            .OrderBy(budget => budget.Category.Name)
            .ToListAsync(cancellationToken);

        var spentByCategory = await GetSpentByCategoryAsync(
            selectedMonth,
            selectedMonth.AddMonths(1).AddDays(-1),
            cancellationToken);

        return Ok(budgets
            .Select(budget => ToResponse(
                budget,
                spentByCategory.GetValueOrDefault(budget.CategoryId)))
            .ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BudgetResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var budget = await _db.Budgets
            .AsNoTracking()
            .Include(candidate => candidate.Category)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (budget is null)
        {
            return NotFound();
        }

        var spent = await GetSpentAsync(
            budget.CategoryId,
            budget.Month,
            cancellationToken);

        return Ok(ToResponse(budget, spent));
    }

    [HttpPost]
    public async Task<ActionResult<BudgetResponse>> Create(
        CreateBudgetRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCreateRequest(request);

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var month = NormalizeMonth(request.Month!.Value);
        var category = await _db.Categories.FirstOrDefaultAsync(
            candidate => candidate.Id == request.CategoryId,
            cancellationToken);

        if (category is null)
        {
            ModelState.AddModelError(
                nameof(CreateBudgetRequest.CategoryId),
                "Category not found.");
        }
        else if (category.Kind != CategoryKind.Expense)
        {
            ModelState.AddModelError(
                nameof(CreateBudgetRequest.CategoryId),
                "Budgets can only use expense categories.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var amount = request.Amount!.Value;
        var existing = await _db.Budgets
            .Include(budget => budget.Category)
            .FirstOrDefaultAsync(
                budget => budget.CategoryId == category!.Id && budget.Month == month,
                cancellationToken);

        if (existing is { IsArchived: false })
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Budget already exists",
                detail: "A budget already exists for this category and month.");
        }

        var now = DateTime.UtcNow;
        Budget budget;

        if (existing is not null)
        {
            existing.IsArchived = false;
            existing.Amount = amount;
            existing.UpdatedAtUtc = now;
            budget = existing;
        }
        else
        {
            budget = new Budget
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.RequireUserId(),
                CategoryId = category!.Id,
                Category = category,
                Month = month,
                Amount = amount,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.Budgets.Add(budget);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var spent = await GetSpentAsync(category!.Id, month, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = budget.Id },
            ToResponse(budget, spent));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BudgetResponse>> Update(
        Guid id,
        UpdateBudgetRequest request,
        CancellationToken cancellationToken)
    {
        ValidateAmount(request.Amount, nameof(UpdateBudgetRequest.Amount));

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var budget = await _db.Budgets
            .Include(candidate => candidate.Category)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (budget is null)
        {
            return NotFound();
        }

        if (budget.IsArchived)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Budget is archived",
                detail: "Archived budgets cannot be updated.");
        }

        budget.Amount = request.Amount!.Value;
        budget.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var spent = await GetSpentAsync(budget.CategoryId, budget.Month, cancellationToken);

        return Ok(ToResponse(budget, spent));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var budget = await _db.Budgets.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);

        if (budget is null)
        {
            return NotFound();
        }

        if (!budget.IsArchived)
        {
            budget.IsArchived = true;
            budget.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private void ValidateCreateRequest(CreateBudgetRequest request)
    {
        if (request.CategoryId == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(CreateBudgetRequest.CategoryId),
                "CategoryId is required.");
        }

        if (request.Month is null || request.Month == default)
        {
            ModelState.AddModelError(
                nameof(CreateBudgetRequest.Month),
                "Month is required.");
        }

        ValidateAmount(request.Amount, nameof(CreateBudgetRequest.Amount));
    }

    private void ValidateAmount(decimal? amount, string fieldName)
    {
        if (amount is null || amount <= 0)
        {
            ModelState.AddModelError(fieldName, "Amount must be greater than zero.");
        }
        else if (decimal.Round(amount.Value, 2) != amount.Value)
        {
            ModelState.AddModelError(
                fieldName,
                "Amount must have at most 2 decimal places.");
        }
        else if (amount > MaxAmount)
        {
            ModelState.AddModelError(fieldName, "Amount is too large.");
        }
    }

    private async Task<Dictionary<Guid, decimal>> GetSpentByCategoryAsync(
        DateOnly monthStart,
        DateOnly monthEnd,
        CancellationToken cancellationToken)
    {
        return await _db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Type == TransactionType.Expense &&
                !transaction.IsArchived &&
                !transaction.Account.IsArchived &&
                transaction.CategoryId != null &&
                transaction.Date >= monthStart &&
                transaction.Date <= monthEnd)
            .GroupBy(transaction => transaction.CategoryId!.Value)
            .Select(group => new
            {
                CategoryId = group.Key,
                Spent = group.Sum(transaction => transaction.Amount)
            })
            .ToDictionaryAsync(
                item => item.CategoryId,
                item => item.Spent,
                cancellationToken);
    }

    private Task<decimal> GetSpentAsync(
        Guid categoryId,
        DateOnly month,
        CancellationToken cancellationToken)
    {
        var monthStart = NormalizeMonth(month);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        return _db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.CategoryId == categoryId &&
                transaction.Type == TransactionType.Expense &&
                !transaction.IsArchived &&
                !transaction.Account.IsArchived &&
                transaction.Date >= monthStart &&
                transaction.Date <= monthEnd)
            .SumAsync(transaction => transaction.Amount, cancellationToken);
    }

    private static DateOnly NormalizeMonth(DateOnly month)
    {
        return new DateOnly(month.Year, month.Month, 1);
    }

    private static BudgetResponse ToResponse(Budget budget, decimal spent)
    {
        var remaining = budget.Amount - spent;
        var percentUsed = decimal.Round(
            spent / budget.Amount * 100m,
            2,
            MidpointRounding.AwayFromZero);

        return new BudgetResponse(
            budget.Id,
            budget.CategoryId,
            budget.Category.Name,
            budget.Month,
            budget.Amount,
            spent,
            remaining,
            percentUsed,
            budget.IsArchived,
            budget.CreatedAtUtc,
            budget.UpdatedAtUtc);
    }
}
