using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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

        group.MapGet(string.Empty, async Task<IResult> (HttpContext httpContext, GroceryListStore store) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            var items = await store.GetAllAsync(householdId);
            return Results.Ok(items);
        });

        group.MapPost(string.Empty, async Task<IResult> (CreateGroceryItemRequest request, HttpContext httpContext, GroceryListStore store) =>
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
            return Results.Created($"/api/grocery/{item.Id}", item);
        });

        group.MapPatch("/{id}", async Task<IResult> (string id, UpdateGroceryItemRequest request, HttpContext httpContext, GroceryListStore store) =>
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

            var item = await store.GetByIdAsync(id);
            if (item is null || item.HouseholdId != householdId)
            {
                return Results.NotFound();
            }

            var updated = await store.UpdateAsync(id, request);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        group.MapDelete("/{id}", async Task<IResult> (string id, HttpContext httpContext, GroceryListStore store) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            var item = await store.GetByIdAsync(id);
            if (item is null || item.HouseholdId != householdId)
            {
                return Results.NotFound();
            }

            var deleted = await store.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/purchased", async Task<IResult> (HttpContext httpContext, GroceryListStore store) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            await store.ClearPurchasedAsync(householdId);
            return Results.NoContent();
        });

        return endpoints;
    }
}
