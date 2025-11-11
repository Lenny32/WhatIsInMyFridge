using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
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

        group.MapGet(string.Empty, async Task<IResult> (HttpContext httpContext, RecipeStore store, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Getting recipes for household {HouseholdId}", householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized access to recipes endpoint");
                return Results.Unauthorized();
            }

            var recipes = await store.GetRecipesAsync(householdId, cancellationToken);
            logger.LogInformation("Retrieved {RecipeCount} recipes for household {HouseholdId}", recipes.Count, householdId);
            return Results.Ok(recipes);
        });

        group.MapGet("/{id}", async Task<IResult> (Guid id, HttpContext httpContext, RecipeStore store, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Getting recipe {RecipeId} for household {HouseholdId}", id, householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized access to recipe {RecipeId}", id);
                return Results.Unauthorized();
            }

            var recipe = await store.GetByIdAsync(id, cancellationToken);
            if (recipe is null || recipe.HouseholdId != householdId)
            {
                logger.LogWarning("Recipe {RecipeId} not found or unauthorized for household {HouseholdId}", id, householdId);
                return Results.NotFound();
            }

            return Results.Ok(recipe);
        });

        group.MapPost(string.Empty, async Task<IResult> (CreateRecipeRequest request, HttpContext httpContext, RecipeStore store, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Creating recipe '{RecipeName}' for household {HouseholdId}", request.Name, householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized attempt to create recipe");
                return Results.Unauthorized();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                logger.LogWarning("Invalid recipe creation request for household {HouseholdId}", householdId);
                return Results.ValidationProblem(errors);
            }

            var recipe = await store.CreateAsync(householdId, request, cancellationToken);
            logger.LogInformation("Created recipe {RecipeId} for household {HouseholdId}", recipe.Id, householdId);
            return Results.Created($"/api/recipes/{recipe.Id}", recipe);
        });

        group.MapPatch("/{id}", async Task<IResult> (Guid id, UpdateRecipeRequest request, HttpContext httpContext, RecipeStore store, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Updating recipe {RecipeId} for household {HouseholdId}", id, householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized attempt to update recipe {RecipeId}", id);
                return Results.Unauthorized();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                logger.LogWarning("Invalid recipe update request for recipe {RecipeId}", id);
                return Results.ValidationProblem(errors);
            }

            var recipe = await store.GetByIdAsync(id, cancellationToken);
            if (recipe is null || recipe.HouseholdId != householdId)
            {
                logger.LogWarning("Recipe {RecipeId} not found or unauthorized for household {HouseholdId}", id, householdId);
                return Results.NotFound();
            }

            var updated = await store.UpdateAsync(id, request, cancellationToken);
            logger.LogInformation("Successfully updated recipe {RecipeId}", id);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        group.MapDelete("/{id}", async Task<IResult> (Guid id, HttpContext httpContext, RecipeStore store, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Deleting recipe {RecipeId} for household {HouseholdId}", id, householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized attempt to delete recipe {RecipeId}", id);
                return Results.Unauthorized();
            }

            var recipe = await store.GetByIdAsync(id, cancellationToken);
            if (recipe is null || recipe.HouseholdId != householdId)
            {
                logger.LogWarning("Recipe {RecipeId} not found or unauthorized for household {HouseholdId}", id, householdId);
                return Results.NotFound();
            }

            var deleted = await store.DeleteAsync(id, cancellationToken);
            
            if (deleted)
            {
                logger.LogInformation("Successfully deleted recipe {RecipeId}", id);
            }
            
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }

    private static void MapIngredientSuggestions(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ingredients");
        group.RequireAuthorization();

        group.MapGet("/suggestions", async Task<IResult> (string? query, HttpContext httpContext, RecipeStore store, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Getting ingredient suggestions for household {HouseholdId}, query: {Query}", householdIdString, query);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized access to ingredient suggestions");
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                logger.LogDebug("Query too short for ingredient suggestions: {Query}", query);
                return Results.Ok(Array.Empty<string>());
            }

            var suggestions = await store.GetIngredientSuggestionsAsync(householdId, query, cancellationToken);
            logger.LogInformation("Retrieved {SuggestionCount} ingredient suggestions for household {HouseholdId}", suggestions.Count, householdId);
            return Results.Ok(suggestions);
        });
    }

    private static void MapRecipePhotos(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/recipes");
        group.RequireAuthorization();

        group.MapPost("/{id}/photos", async Task<IResult> (Guid id, IFormFile file, HttpContext httpContext, RecipeStore store, BlobStorageService blobStorage, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            var userIdString = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Uploading photo for recipe {RecipeId}, household {HouseholdId}, user {UserId}", id, householdIdString, userIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized attempt to upload photo for recipe {RecipeId}", id);
                return Results.Unauthorized();
            }

            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                logger.LogWarning("Unauthorized attempt to upload photo for recipe {RecipeId}", id);
                return Results.Unauthorized();
            }

            var recipe = await store.GetByIdAsync(id, cancellationToken);
            if (recipe == null || recipe.HouseholdId != householdId)
            {
                logger.LogWarning("Recipe {RecipeId} not found or unauthorized for household {HouseholdId}", id, householdId);
                return Results.NotFound();
            }

            if (recipe.Photos.Count >= 4)
            {
                logger.LogWarning("Maximum photos reached for recipe {RecipeId}", id);
                return Results.BadRequest(new { error = "Maximum 4 photos allowed per recipe" });
            }

            if (file.Length == 0)
            {
                logger.LogWarning("Empty file upload attempt for recipe {RecipeId}", id);
                return Results.BadRequest(new { error = "Empty file" });
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                logger.LogWarning("File too large for recipe {RecipeId}: {FileSize} bytes", id, file.Length);
                return Results.BadRequest(new { error = "File too large. Maximum size is 5MB" });
            }

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                logger.LogWarning("Invalid file type for recipe {RecipeId}: {ContentType}", id, file.ContentType);
                return Results.BadRequest(new { error = "Invalid file type. Only JPEG, PNG, and WebP images are allowed" });
            }

            // Generate filename in format: UserId_yyyyMMddHHmmss.{ext}
            var extension = Path.GetExtension(file.FileName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var fileName = $"{userId}_{timestamp}{extension}";
            var originalFileName = file.FileName;

            using (var stream = file.OpenReadStream())
            {
                await blobStorage.UploadPhotoAsync(stream, fileName, file.ContentType, cancellationToken);
            }

            var recipePhoto = new RecipePhoto
            {
                FileName = fileName,
                OriginalFileName = originalFileName,
                UploadedAt = DateTimeOffset.UtcNow
            };

            recipe.Photos.Add(recipePhoto);
            recipe.UpdatedAt = DateTimeOffset.UtcNow;
            await store.UpdatePhotosAsync(id, recipe.Photos, cancellationToken);
            
            logger.LogInformation("Successfully uploaded photo {FileName} (original: {OriginalFileName}) for recipe {RecipeId}", fileName, originalFileName, id);

            return Results.Ok(new { fileName, originalFileName, url = $"/api/photos/{fileName}" });
        }).DisableAntiforgery();

        group.MapDelete("/{id}/photos/{fileName}", async Task<IResult> (Guid id, string fileName, HttpContext httpContext, RecipeStore store, BlobStorageService blobStorage, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var householdIdString = httpContext.User.FindFirst("householdId")?.Value;
            logger.LogInformation("Deleting photo {FileName} from recipe {RecipeId}, household {HouseholdId}", fileName, id, householdIdString);
            
            if (string.IsNullOrEmpty(householdIdString) || !Guid.TryParse(householdIdString, out Guid householdId))
            {
                logger.LogWarning("Unauthorized attempt to delete photo {FileName} from recipe {RecipeId}", fileName, id);
                return Results.Unauthorized();
            }

            var recipe = await store.GetByIdAsync(id, cancellationToken);
            if (recipe == null || recipe.HouseholdId != householdId)
            {
                logger.LogWarning("Recipe {RecipeId} not found or unauthorized for household {HouseholdId}", id, householdId);
                return Results.NotFound();
            }

            var photo = recipe.Photos.FirstOrDefault(p => p.FileName == fileName);
            if (photo == null)
            {
                logger.LogWarning("Photo {FileName} not found in recipe {RecipeId}", fileName, id);
                return Results.NotFound();
            }

            await blobStorage.DeletePhotoAsync(Path.GetFileNameWithoutExtension(fileName), cancellationToken);

            recipe.Photos.Remove(photo);
            recipe.UpdatedAt = DateTimeOffset.UtcNow;
            await store.UpdatePhotosAsync(id, recipe.Photos, cancellationToken);
            
            logger.LogInformation("Successfully deleted photo {FileName} from recipe {RecipeId}", fileName, id);

            return Results.NoContent();
        });
    }

    private static void MapPhotoServing(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/photos/{fileName}", async Task<IResult> (string fileName, BlobStorageService blobStorage, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            logger.LogDebug("Serving photo {FileName}", fileName);
            
            var photoData = await blobStorage.GetPhotoAsync(fileName, cancellationToken);
            if (photoData == null)
            {
                logger.LogWarning("Photo {FileName} not found", fileName);
                return Results.NotFound();
            }

            return Results.File(photoData.Value.fileBytes, photoData.Value.contentType);
        });
    }
}
