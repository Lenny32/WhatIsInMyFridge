using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class MealPlanStore
{
    private readonly AppDbContext _context;
    private readonly ILogger<MealPlanStore> _logger;

    public MealPlanStore(AppDbContext context, ILogger<MealPlanStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<MealPlan>> GetMealPlansAsync(Guid householdId, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving meal plans for household {HouseholdId}", householdId);
        
        var query = _context.MealPlans.Where(mp => mp.HouseholdId == householdId);
        
        if (startDate.HasValue)
        {
            query = query.Where(mp => mp.PlannedDate >= startDate.Value);
        }
        
        if (endDate.HasValue)
        {
            query = query.Where(mp => mp.PlannedDate <= endDate.Value);
        }
        
        var mealPlans = await query.ToArrayAsync(cancellationToken);
        
        _logger.LogDebug("Retrieved {Count} meal plans for household {HouseholdId}", mealPlans.Length, householdId);
        return mealPlans.OrderBy(mp => mp.PlannedDate).ThenBy(mp => mp.MealName).ToArray();
    }

    public async Task<MealPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.MealPlans
            .FirstOrDefaultAsync(mp => mp.Id == id, cancellationToken);
    }

    public async Task<MealPlan> CreateAsync(Guid householdId, CreateMealPlanRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating meal plan for household {HouseholdId} on {PlannedDate}", householdId, request.PlannedDate);
        var now = DateTimeOffset.UtcNow;
        var mealPlan = new MealPlan
        {
            HouseholdId = householdId,
            RecipeId = request.RecipeId,
            PlannedDate = request.PlannedDate,
            MealName = request.MealName?.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.MealPlans.Add(mealPlan);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Meal plan {MealPlanId} created successfully", mealPlan.Id);
        return mealPlan;
    }

    public async Task<MealPlan?> UpdateAsync(Guid id, UpdateMealPlanRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating meal plan {MealPlanId}", id);
        var existing = await _context.MealPlans
            .FirstOrDefaultAsync(mp => mp.Id == id, cancellationToken);
        
        if (existing == null)
        {
            _logger.LogWarning("Update failed: Meal plan {MealPlanId} not found", id);
            return null;
        }

        if (request.RecipeId.HasValue)
            existing.RecipeId = request.RecipeId.Value;
        
        if (request.PlannedDate.HasValue)
            existing.PlannedDate = request.PlannedDate.Value;
        
        if (request.MealName != null)
            existing.MealName = request.MealName.Trim();
        
        if (request.Notes != null)
            existing.Notes = request.Notes.Trim();

        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Meal plan {MealPlanId} updated successfully", id);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting meal plan {MealPlanId}", id);
        var mealPlan = await _context.MealPlans.FindAsync(new object[] { id }, cancellationToken);
        
        if (mealPlan == null)
        {
            _logger.LogWarning("Delete failed: Meal plan {MealPlanId} not found", id);
            return false;
        }

        _context.MealPlans.Remove(mealPlan);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Meal plan {MealPlanId} deleted successfully", id);
        return true;
    }
}
