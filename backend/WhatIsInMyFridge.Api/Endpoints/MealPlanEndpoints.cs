using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Models;
using WhatIsInMyFridge.Api.Services;

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class MealPlanEndpoints
{
    public static IEndpointRouteBuilder MapMealPlanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/mealplans");
        group.RequireAuthorization();

        group.MapGet(string.Empty, async Task<IResult> (
            HttpContext httpContext, 
            MealPlanStore store, 
            RecipeStore recipeStore,
            ILogger<Program> logger, 
            DateOnly? startDate,
            DateOnly? endDate,
            CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Getting meal plans for household {HouseholdId}", householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized access to meal plans endpoint");
                return Results.Unauthorized();
            }

            var mealPlans = await store.GetMealPlansAsync(householdId, startDate, endDate, cancellationToken);
            
            // Fetch all recipes for the household to enrich meal plan responses
            var recipes = await recipeStore.GetRecipesAsync(householdId, cancellationToken);
            var recipeDict = recipes.ToDictionary(r => r.Id, r => r);
            
            // Create enriched response with recipe details
            var enrichedMealPlans = mealPlans.Select(mp => new
            {
                mp.Id,
                mp.HouseholdId,
                mp.RecipeId,
                Recipe = recipeDict.GetValueOrDefault(mp.RecipeId),
                mp.PlannedDate,
                mp.MealName,
                mp.Notes,
                mp.CreatedAt,
                mp.UpdatedAt
            });
            
            logger.LogInformation("Retrieved {MealPlanCount} meal plans for household {HouseholdId}", mealPlans.Count, householdId);
            return Results.Ok(enrichedMealPlans);
        });

        group.MapGet("/{id}", async Task<IResult> (
            Guid id, 
            HttpContext httpContext, 
            MealPlanStore store, 
            RecipeStore recipeStore,
            ILogger<Program> logger, 
            CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Getting meal plan {MealPlanId} for household {HouseholdId}", id, householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized access to meal plan {MealPlanId}", id);
                return Results.Unauthorized();
            }

            var mealPlan = await store.GetByIdAsync(id, cancellationToken);
            
            if (mealPlan == null || mealPlan.HouseholdId != householdId)
            {
                logger.LogWarning("Meal plan {MealPlanId} not found or unauthorized", id);
                return Results.NotFound();
            }

            // Fetch the associated recipe
            var recipe = await recipeStore.GetByIdAsync(mealPlan.RecipeId, cancellationToken);
            
            var enrichedMealPlan = new
            {
                mealPlan.Id,
                mealPlan.HouseholdId,
                mealPlan.RecipeId,
                Recipe = recipe,
                mealPlan.PlannedDate,
                mealPlan.MealName,
                mealPlan.Notes,
                mealPlan.CreatedAt,
                mealPlan.UpdatedAt
            };

            logger.LogInformation("Retrieved meal plan {MealPlanId}", id);
            return Results.Ok(enrichedMealPlan);
        });

        group.MapPost(string.Empty, async Task<IResult> (
            CreateMealPlanRequest request, 
            HttpContext httpContext, 
            MealPlanStore store,
            RecipeStore recipeStore,
            ILogger<Program> logger, 
            CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Creating meal plan for household {HouseholdId}", householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized access to create meal plan");
                return Results.Unauthorized();
            }

            // Verify the recipe exists and belongs to the household
            var recipe = await recipeStore.GetByIdAsync(request.RecipeId, cancellationToken);
            if (recipe == null || recipe.HouseholdId != householdId)
            {
                logger.LogWarning("Recipe {RecipeId} not found or unauthorized", request.RecipeId);
                return Results.BadRequest(new { error = "Recipe not found or unauthorized" });
            }

            var mealPlan = await store.CreateAsync(householdId, request, cancellationToken);
            
            var enrichedMealPlan = new
            {
                mealPlan.Id,
                mealPlan.HouseholdId,
                mealPlan.RecipeId,
                Recipe = recipe,
                mealPlan.PlannedDate,
                mealPlan.MealName,
                mealPlan.Notes,
                mealPlan.CreatedAt,
                mealPlan.UpdatedAt
            };
            
            logger.LogInformation("Meal plan {MealPlanId} created successfully", mealPlan.Id);
            return Results.Created($"/api/mealplans/{mealPlan.Id}", enrichedMealPlan);
        });

        group.MapPatch("/{id}", async Task<IResult> (
            Guid id, 
            UpdateMealPlanRequest request, 
            HttpContext httpContext, 
            MealPlanStore store,
            RecipeStore recipeStore,
            ILogger<Program> logger, 
            CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Updating meal plan {MealPlanId} for household {HouseholdId}", id, householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized access to update meal plan {MealPlanId}", id);
                return Results.Unauthorized();
            }

            var existing = await store.GetByIdAsync(id, cancellationToken);
            if (existing == null || existing.HouseholdId != householdId)
            {
                logger.LogWarning("Meal plan {MealPlanId} not found or unauthorized", id);
                return Results.NotFound();
            }

            // If recipe is being changed, verify it exists and belongs to the household
            if (request.RecipeId.HasValue)
            {
                var recipe = await recipeStore.GetByIdAsync(request.RecipeId.Value, cancellationToken);
                if (recipe == null || recipe.HouseholdId != householdId)
                {
                    logger.LogWarning("Recipe {RecipeId} not found or unauthorized", request.RecipeId.Value);
                    return Results.BadRequest(new { error = "Recipe not found or unauthorized" });
                }
            }

            var updated = await store.UpdateAsync(id, request, cancellationToken);
            
            if (updated == null)
            {
                logger.LogWarning("Failed to update meal plan {MealPlanId}", id);
                return Results.NotFound();
            }

            // Fetch the associated recipe for enriched response
            var recipeForResponse = await recipeStore.GetByIdAsync(updated.RecipeId, cancellationToken);
            
            var enrichedMealPlan = new
            {
                updated.Id,
                updated.HouseholdId,
                updated.RecipeId,
                Recipe = recipeForResponse,
                updated.PlannedDate,
                updated.MealName,
                updated.Notes,
                updated.CreatedAt,
                updated.UpdatedAt
            };

            logger.LogInformation("Meal plan {MealPlanId} updated successfully", id);
            return Results.Ok(enrichedMealPlan);
        });

        group.MapDelete("/{id}", async Task<IResult> (
            Guid id, 
            HttpContext httpContext, 
            MealPlanStore store, 
            ILogger<Program> logger, 
            CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Deleting meal plan {MealPlanId} for household {HouseholdId}", id, householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized access to delete meal plan {MealPlanId}", id);
                return Results.Unauthorized();
            }

            var existing = await store.GetByIdAsync(id, cancellationToken);
            if (existing == null || existing.HouseholdId != householdId)
            {
                logger.LogWarning("Meal plan {MealPlanId} not found or unauthorized", id);
                return Results.NotFound();
            }

            var deleted = await store.DeleteAsync(id, cancellationToken);
            
            if (!deleted)
            {
                logger.LogWarning("Failed to delete meal plan {MealPlanId}", id);
                return Results.NotFound();
            }

            logger.LogInformation("Meal plan {MealPlanId} deleted successfully", id);
            return Results.NoContent();
        });

        return endpoints;
    }
}
