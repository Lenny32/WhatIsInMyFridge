using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Infrastructure;
using WhatIsInMyFridge.Api.Services;

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class RecipeEndpoints
{
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapRecipeCrud(endpoints);
        MapIngredientSuggestions(endpoints);
        MapRecipePhotos(endpoints);
        MapPhotoServing(endpoints);

        return endpoints;
    }

    private static void MapRecipeCrud(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/recipes");
        group.RequireAuthorization();

        group.MapGet(string.Empty, async Task<IResult> (HttpContext httpContext, RecipeStore store) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            var recipes = await store.GetRecipesAsync(householdId);
            return Results.Ok(recipes);
        });

        group.MapGet("/{id}", async Task<IResult> (string id, HttpContext httpContext, RecipeStore store) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            var recipe = await store.GetByIdAsync(id);
            if (recipe is null || recipe.HouseholdId != householdId)
            {
                return Results.NotFound();
            }

            return Results.Ok(recipe);
        });

        group.MapPost(string.Empty, async Task<IResult> (CreateRecipeRequest request, HttpContext httpContext, RecipeStore store) =>
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

            var recipe = await store.CreateAsync(householdId, request);
            return Results.Created($"/api/recipes/{recipe.Id}", recipe);
        });

        group.MapPatch("/{id}", async Task<IResult> (string id, UpdateRecipeRequest request, HttpContext httpContext, RecipeStore store) =>
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

            var recipe = await store.GetByIdAsync(id);
            if (recipe is null || recipe.HouseholdId != householdId)
            {
                return Results.NotFound();
            }

            var updated = await store.UpdateAsync(id, request);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        group.MapDelete("/{id}", async Task<IResult> (string id, HttpContext httpContext, RecipeStore store) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            var recipe = await store.GetByIdAsync(id);
            if (recipe is null || recipe.HouseholdId != householdId)
            {
                return Results.NotFound();
            }

            var deleted = await store.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }

    private static void MapIngredientSuggestions(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ingredients");
        group.RequireAuthorization();

        group.MapGet("/suggestions", async Task<IResult> (string? query, HttpContext httpContext, RecipeStore store) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Results.Ok(Array.Empty<string>());
            }

            var suggestions = await store.GetIngredientSuggestionsAsync(householdId, query);
            return Results.Ok(suggestions);
        });
    }

    private static void MapRecipePhotos(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/recipes");
        group.RequireAuthorization();

        group.MapPost("/{id}/photos", async Task<IResult> (string id, IFormFile file, HttpContext httpContext, RecipeStore store, BlobStorageService blobStorage) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            var recipe = await store.GetByIdAsync(id);
            if (recipe == null || recipe.HouseholdId != householdId)
            {
                return Results.NotFound();
            }

            if (recipe.Photos.Count >= 4)
            {
                return Results.BadRequest(new { error = "Maximum 4 photos allowed per recipe" });
            }

            if (file.Length == 0)
            {
                return Results.BadRequest(new { error = "Empty file" });
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                return Results.BadRequest(new { error = "File too large. Maximum size is 5MB" });
            }

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return Results.BadRequest(new { error = "Invalid file type. Only JPEG, PNG, and WebP images are allowed" });
            }

            var extension = Path.GetExtension(file.FileName);
            var photoId = Guid.NewGuid().ToString("N");
            var fileName = $"{photoId}{extension}";

            using (var stream = file.OpenReadStream())
            {
                await blobStorage.UploadPhotoAsync(stream, fileName, file.ContentType);
            }

            recipe.Photos.Add(photoId);
            recipe.UpdatedAt = DateTimeOffset.UtcNow;
            await store.UpdatePhotosAsync(id, recipe.Photos);

            return Results.Ok(new { photoId, url = $"/api/photos/{photoId}{extension}" });
        }).DisableAntiforgery();

        group.MapDelete("/{id}/photos/{photoId}", async Task<IResult> (string id, string photoId, HttpContext httpContext, RecipeStore store, BlobStorageService blobStorage) =>
        {
            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.Unauthorized();
            }

            var recipe = await store.GetByIdAsync(id);
            if (recipe == null || recipe.HouseholdId != householdId)
            {
                return Results.NotFound();
            }

            if (!recipe.Photos.Contains(photoId))
            {
                return Results.NotFound();
            }

            await blobStorage.DeletePhotoAsync(photoId);

            recipe.Photos.Remove(photoId);
            recipe.UpdatedAt = DateTimeOffset.UtcNow;
            await store.UpdatePhotosAsync(id, recipe.Photos);

            return Results.NoContent();
        });
    }

    private static void MapPhotoServing(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/photos/{fileName}", async Task<IResult> (string fileName, BlobStorageService blobStorage) =>
        {
            var photoData = await blobStorage.GetPhotoAsync(fileName);
            if (photoData == null)
            {
                return Results.NotFound();
            }

            return Results.File(photoData.Value.fileBytes, photoData.Value.contentType);
        });
    }
}
