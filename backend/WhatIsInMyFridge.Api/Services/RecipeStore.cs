using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class RecipeStore
{
    private readonly AppDbContext _context;
    private readonly ILogger<RecipeStore> _logger;

    public RecipeStore(AppDbContext context, ILogger<RecipeStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<Recipe>> GetRecipesAsync(string householdId)
    {
        _logger.LogDebug("Retrieving recipes for household {HouseholdId}", householdId);
        var recipes = await _context.Recipes
            .Include(r => r.Ingredients)
            .Where(r => r.HouseholdId == householdId)
            .ToArrayAsync();
        
        _logger.LogDebug("Retrieved {Count} recipes for household {HouseholdId}", recipes.Length, householdId);
        return recipes.OrderByDescending(r => r.CreatedAt).ToArray();
    }

    public async Task<Recipe?> GetByIdAsync(string id)
    {
        return await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Recipe> CreateAsync(string householdId, CreateRecipeRequest request)
    {
        _logger.LogInformation("Creating recipe {Name} for household {HouseholdId}", request.Name, householdId);
        var now = DateTimeOffset.UtcNow;
        var recipe = new Recipe
        {
            Id = Guid.NewGuid().ToString("N"),
            HouseholdId = householdId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Servings = request.Servings,
            PrepTimeMinutes = request.PrepTimeMinutes,
            CookTimeMinutes = request.CookTimeMinutes,
            Ingredients = request.Ingredients.Select(i => new RecipeIngredient
            {
                Name = i.Name.Trim(),
                Quantity = i.Quantity,
                Unit = i.Unit,
                Notes = i.Notes?.Trim()
            }).ToList(),
            Instructions = request.Instructions.Select(i => i.Trim()).ToList(),
            Notes = request.Notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Recipe {RecipeId} created successfully", recipe.Id);
        return recipe;
    }

    public async Task<Recipe?> UpdateAsync(string id, UpdateRecipeRequest request)
    {
        _logger.LogInformation("Updating recipe {RecipeId}", id);
        var existing = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == id);
        
        if (existing == null)
        {
            _logger.LogWarning("Update failed: Recipe {RecipeId} not found", id);
            return null;
        }

        existing.Name = request.Name is { Length: > 0 } name ? name.Trim() : existing.Name;
        existing.Description = request.Description is { Length: > 0 } desc ? desc.Trim() : request.Description == string.Empty ? null : existing.Description;
        existing.Servings = request.Servings ?? existing.Servings;
        existing.PrepTimeMinutes = request.PrepTimeMinutes ?? existing.PrepTimeMinutes;
        existing.CookTimeMinutes = request.CookTimeMinutes ?? existing.CookTimeMinutes;
        existing.Notes = request.Notes is { Length: > 0 } notes ? notes.Trim() : request.Notes == string.Empty ? null : existing.Notes;
        
        if (request.Ingredients != null)
        {
            // With owned entities, just replace the list - EF will handle the rest
            existing.Ingredients = request.Ingredients.Select(i => new RecipeIngredient
            {
                RecipeId = existing.Id,
                Name = i.Name.Trim(),
                Quantity = i.Quantity,
                Unit = i.Unit,
                Notes = i.Notes?.Trim()
            }).ToList();
        }

        if (request.Instructions != null)
        {
            existing.Instructions = request.Instructions.Select(i => i.Trim()).ToList();
        }

        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Recipe {RecipeId} updated successfully", id);
        return existing;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        _logger.LogInformation("Deleting recipe {RecipeId}", id);
        var recipe = await _context.Recipes
            .Where(r => r.Id == id)
            .FirstOrDefaultAsync();
        if (recipe == null)
        {
            _logger.LogWarning("Delete failed: Recipe {RecipeId} not found", id);
            return false;
        }

        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Recipe {RecipeId} deleted successfully", id);
        return true;
    }

    public async Task<IReadOnlyCollection<string>> GetIngredientSuggestionsAsync(string householdId, string query)
    {
        // Get unique ingredient names from user's food items
        var suggestions = await _context.FoodItems
            .Where(f => f.HouseholdId == householdId && f.Name.ToLower().Contains(query.ToLower()))
            .Select(f => f.Name)
            .Distinct()
            .OrderBy(n => n)
            .Take(10)
            .ToArrayAsync();

        return suggestions;
    }

    public async Task UpdatePhotosAsync(string id, List<string> photos)
    {
        _logger.LogInformation("Updating photos for recipe {RecipeId}", id);
        var recipe = await _context.Recipes
            .Where(r => r.Id == id)
            .FirstOrDefaultAsync();
        if (recipe == null)
        {
            _logger.LogWarning("Update photos failed: Recipe {RecipeId} not found", id);
            return;
        }

        recipe.Photos = photos;
        recipe.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Photos updated for recipe {RecipeId}", id);
    }
}
