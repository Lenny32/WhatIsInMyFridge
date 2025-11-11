using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Infrastructure;
using WhatIsInMyFridge.Api.Services;

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class GroceryEndpoints
{
    public static IEndpointRouteBuilder MapGroceryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/grocery");
        group.RequireAuthorization();

        group.MapGet(string.Empty, async Task<IResult> (HttpContext httpContext, GroceryListStore store, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Getting grocery list for household {HouseholdId}", householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized access to grocery list");
                return Results.Unauthorized();
            }

            var items = await store.GetAllAsync(householdId, cancellationToken);
            logger.LogInformation("Retrieved {ItemCount} grocery items for household {HouseholdId}", items.Count, householdId);
            return Results.Ok(items);
        });

        group.MapPost(string.Empty, async Task<IResult> (CreateGroceryItemRequest request, HttpContext httpContext, GroceryListStore store, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Creating grocery item for household {HouseholdId}: {ItemName}", householdIdString, request.Name);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized attempt to create grocery item");
                return Results.Unauthorized();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                logger.LogWarning("Invalid grocery item creation request for household {HouseholdId}", householdId);
                return Results.ValidationProblem(errors);
            }

            var item = await store.CreateAsync(householdId, request, cancellationToken);
            logger.LogInformation("Created grocery item {ItemId} for household {HouseholdId}", item.Id, householdId);
            return Results.Created($"/api/grocery/{item.Id}", item);
        });

        group.MapPatch("/{id}", async Task<IResult> (Guid id, UpdateGroceryItemRequest request, HttpContext httpContext, GroceryListStore store, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Updating grocery item {ItemId} for household {HouseholdId}", id, householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized attempt to update grocery item {ItemId}", id);
                return Results.Unauthorized();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                logger.LogWarning("Invalid grocery item update request for item {ItemId}", id);
                return Results.ValidationProblem(errors);
            }

            var item = await store.GetByIdAsync(id, cancellationToken);
            if (item is null || item.HouseholdId != householdId)
            {
                logger.LogWarning("Grocery item {ItemId} not found or unauthorized for household {HouseholdId}", id, householdId);
                return Results.NotFound();
            }

            var updated = await store.UpdateAsync(id, request, cancellationToken);
            logger.LogInformation("Successfully updated grocery item {ItemId}", id);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        group.MapDelete("/{id}", async Task<IResult> (Guid id, HttpContext httpContext, GroceryListStore store, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Deleting grocery item {ItemId} for household {HouseholdId}", id, householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized attempt to delete grocery item {ItemId}", id);
                return Results.Unauthorized();
            }

            var item = await store.GetByIdAsync(id, cancellationToken);
            if (item is null || item.HouseholdId != householdId)
            {
                logger.LogWarning("Grocery item {ItemId} not found or unauthorized for household {HouseholdId}", id, householdId);
                return Results.NotFound();
            }

            var deleted = await store.DeleteAsync(id, cancellationToken);
            
            if (deleted)
            {
                logger.LogInformation("Successfully deleted grocery item {ItemId}", id);
            }
            
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/purchased", async Task<IResult> (HttpContext httpContext, GroceryListStore store, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Clearing purchased items for household {HouseholdId}", householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized attempt to clear purchased items");
                return Results.Unauthorized();
            }

            await store.ClearPurchasedAsync(householdId, cancellationToken);
            logger.LogInformation("Successfully cleared purchased items for household {HouseholdId}", householdId);
            return Results.NoContent();
        });

        return endpoints;
    }
}
