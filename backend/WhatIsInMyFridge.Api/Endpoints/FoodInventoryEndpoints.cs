using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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

        group.MapGet(string.Empty, async Task<IResult> (string? location, HttpContext httpContext, FoodInventoryStore store) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            StorageLocation? parsedLocation = null;

            if (!string.IsNullOrWhiteSpace(location))
            {
                if (!Enum.TryParse<StorageLocation>(location, true, out var parsed))
                {
                    return Results.BadRequest(new
                    {
                        error = "Invalid location. Use fridge, freezer, or pantry."
                    });
                }

                parsedLocation = parsed;
            }

            var items = await store.GetItemsAsync(householdId, parsedLocation);
            return Results.Ok(items);
        });

        group.MapGet("/to-buy", async Task<IResult> (HttpContext httpContext, FoodInventoryStore store) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            var items = await store.GetToBuyListAsync(householdId);
            return Results.Ok(items);
        });

        group.MapGet("/{id}", async Task<IResult> (string id, FoodInventoryStore store) =>
        {
            var item = await store.GetByIdAsync(id);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        group.MapPost(string.Empty, async Task<IResult> (CreateFoodItemRequest request, HttpContext httpContext, FoodInventoryStore store) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                return Results.ValidationProblem(errors);
            }

            var item = await store.CreateAsync(householdId, request);
            return Results.Created($"/api/items/{item.Id}", item);
        });

        group.MapPatch("/{id}", async Task<IResult> (string id, UpdateFoodItemRequest request, FoodInventoryStore store) =>
        {
            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                return Results.ValidationProblem(errors);
            }

            var updated = await store.UpdateAsync(id, request);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        group.MapDelete("/{id}", async Task<IResult> (string id, FoodInventoryStore store) =>
        {
            var deleted = await store.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return endpoints;
    }
}
