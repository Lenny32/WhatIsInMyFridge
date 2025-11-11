using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Infrastructure;
using WhatIsInMyFridge.Api.Models;
using WhatIsInMyFridge.Api.Services;

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class FoodInventoryEndpoints
{
    public static IEndpointRouteBuilder MapFoodInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/items");
        group.RequireAuthorization();

        group.MapGet(string.Empty, async Task<IResult> (string? location, HttpContext httpContext, FoodInventoryStore store, ILogger<Program> logger) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Getting food inventory for household {HouseholdId}, location: {Location}", householdIdString, location ?? "all");
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized access to food inventory");
                return Results.Unauthorized();
            }

            StorageLocation? parsedLocation = null;

            if (!string.IsNullOrWhiteSpace(location))
            {
                if (!Enum.TryParse<StorageLocation>(location, true, out var parsed))
                {
                    logger.LogWarning("Invalid storage location provided: {Location}", location);
                    return Results.BadRequest(new
                    {
                        error = "Invalid location. Use fridge, freezer, or pantry."
                    });
                }

                parsedLocation = parsed;
            }

            var items = await store.GetItemsAsync(householdId, parsedLocation);
            logger.LogInformation("Retrieved {ItemCount} food items for household {HouseholdId}", items.Count, householdId);
            return Results.Ok(items);
        });

        group.MapGet("/to-buy", async Task<IResult> (HttpContext httpContext, FoodInventoryStore store, ILogger<Program> logger) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Getting to-buy list for household {HouseholdId}", householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized access to to-buy list");
                return Results.Unauthorized();
            }

            var items = await store.GetToBuyListAsync(householdId);
            logger.LogInformation("Retrieved {ItemCount} items in to-buy list for household {HouseholdId}", items.Count, householdId);
            return Results.Ok(items);
        });

        group.MapGet("/{id}", async Task<IResult> (Guid id, FoodInventoryStore store, ILogger<Program> logger) =>
        {
            logger.LogInformation("Getting food item {ItemId}", id);
            var item = await store.GetByIdAsync(id);
            
            if (item is null)
            {
                logger.LogWarning("Food item {ItemId} not found", id);
            }
            
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        group.MapPost(string.Empty, async Task<IResult> (CreateFoodItemRequest request, HttpContext httpContext, FoodInventoryStore store, ILogger<Program> logger) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Creating food item for household {HouseholdId}: {ItemName}", householdIdString, request.Name);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized attempt to create food item");
                return Results.Unauthorized();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                logger.LogWarning("Invalid food item creation request for household {HouseholdId}", householdId);
                return Results.ValidationProblem(errors);
            }

            var item = await store.CreateAsync(householdId, request);
            logger.LogInformation("Created food item {ItemId} for household {HouseholdId}", item.Id, householdId);
            return Results.Created($"/api/items/{item.Id}", item);
        });

        group.MapPatch("/{id}", async Task<IResult> (Guid id, UpdateFoodItemRequest request, FoodInventoryStore store, ILogger<Program> logger) =>
        {
            logger.LogInformation("Updating food item {ItemId}", id);
            
            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                logger.LogWarning("Invalid food item update request for item {ItemId}", id);
                return Results.ValidationProblem(errors);
            }

            var updated = await store.UpdateAsync(id, request);
            
            if (updated is null)
            {
                logger.LogWarning("Failed to update food item {ItemId} - not found", id);
            }
            else
            {
                logger.LogInformation("Successfully updated food item {ItemId}", id);
            }
            
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        group.MapDelete("/{id}", async Task<IResult> (Guid id, FoodInventoryStore store, ILogger<Program> logger) =>
        {
            logger.LogInformation("Deleting food item {ItemId}", id);
            var deleted = await store.DeleteAsync(id);
            
            if (deleted)
            {
                logger.LogInformation("Successfully deleted food item {ItemId}", id);
            }
            else
            {
                logger.LogWarning("Failed to delete food item {ItemId} - not found", id);
            }
            
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return endpoints;
    }
}
