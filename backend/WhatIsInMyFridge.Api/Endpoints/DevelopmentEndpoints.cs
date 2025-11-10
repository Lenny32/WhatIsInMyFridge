using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using WhatIsInMyFridge.Api.Services;

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class DevelopmentEndpoints
{
#if DEBUG
    public static IEndpointRouteBuilder MapDevelopmentEndpoints(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        // Only register these endpoints in Development mode and DEBUG builds
        if (!environment.IsDevelopment())
        {
            return endpoints;
        }

        var group = endpoints.MapGroup("/api/dev");
        group.RequireAuthorization();

        // Database testing endpoint - requires admin access
        group.MapGet("/test-database", async Task<IResult> (HttpContext httpContext, AppDbContext dbContext, UserStore userStore, ILogger<Program> logger) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Database test requested by user: {UserId}", userId);
            
            if (string.IsNullOrEmpty(userId))
            {
                logger.LogWarning("Unauthorized database test attempt");
                return Results.Unauthorized();
            }

            var currentUser = await userStore.GetByIdAsync(userId);
            if (currentUser == null || !currentUser.IsAdmin)
            {
                logger.LogWarning("Non-admin user {UserId} attempted to access database test endpoint", userId);
                return Results.Forbid();
            }

            logger.LogInformation("Admin {UserId} starting database connectivity test", userId);

            var testResults = new List<object>();

            try
            {
                // Test Users table
                logger.LogDebug("Testing Users table");
                var firstUser = await dbContext.Users.Take(1).ToArrayAsync();
                testResults.Add(new
                {
                    Table = "Users",
                    Status = "Success",
                    Count = firstUser.Length,
                    FirstItem = firstUser.Length > 0 ? new { firstUser[0].Id, firstUser[0].Email, firstUser[0].Name, firstUser[0].IsAdmin } : null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error testing Users table");
                testResults.Add(new
                {
                    Table = "Users",
                    Status = "Error",
                    Error = ex.Message
                });
            }

            try
            {
                // Test Households table
                logger.LogDebug("Testing Households table");
                var firstHousehold = await dbContext.Households.Take(1).ToArrayAsync();
                testResults.Add(new
                {
                    Table = "Households",
                    Status = "Success",
                    Count = firstHousehold.Length,
                    FirstItem = firstHousehold.Length > 0 ? new { firstHousehold[0].Id, firstHousehold[0].Name, firstHousehold[0].OwnerId } : null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error testing Households table");
                testResults.Add(new
                {
                    Table = "Households",
                    Status = "Error",
                    Error = ex.Message
                });
            }

            try
            {
                // Test FoodItems table
                logger.LogDebug("Testing FoodItems table");
                var firstFoodItem = await dbContext.FoodItems.Take(1).ToArrayAsync();
                testResults.Add(new
                {
                    Table = "FoodItems",
                    Status = "Success",
                    Count = firstFoodItem.Length,
                    FirstItem = firstFoodItem.Length > 0 ? new { firstFoodItem[0].Id, firstFoodItem[0].Name, firstFoodItem[0].HouseholdId, firstFoodItem[0].Location, firstFoodItem[0].Quantity, firstFoodItem[0].RestockThreshold } : null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error testing FoodItems table");
                testResults.Add(new
                {
                    Table = "FoodItems",
                    Status = "Error",
                    Error = ex.Message
                });
            }

            try
            {
                // Test Recipes table
                logger.LogDebug("Testing Recipes table");
                var firstRecipe = await dbContext.Recipes.Take(1).ToArrayAsync();
                testResults.Add(new
                {
                    Table = "Recipes",
                    Status = "Success",
                    Count = firstRecipe.Length,
                    FirstItem = firstRecipe.Length > 0 ? new { firstRecipe[0].Id, firstRecipe[0].Name, firstRecipe[0].HouseholdId, IngredientsCount = firstRecipe[0].Ingredients.Count } : null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error testing Recipes table");
                testResults.Add(new
                {
                    Table = "Recipes",
                    Status = "Error",
                    Error = ex.Message
                });
            }

            try
            {
                // Test GroceryItems table
                logger.LogDebug("Testing GroceryItems table");
                var firstGroceryItem = await dbContext.GroceryItems.Take(1).ToArrayAsync();
                testResults.Add(new
                {
                    Table = "GroceryItems",
                    Status = "Success",
                    Count = firstGroceryItem.Length,
                    FirstItem = firstGroceryItem.Length > 0 ? new { firstGroceryItem[0].Id, firstGroceryItem[0].Name, firstGroceryItem[0].HouseholdId, firstGroceryItem[0].IsPurchased } : null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error testing GroceryItems table");
                testResults.Add(new
                {
                    Table = "GroceryItems",
                    Status = "Error",
                    Error = ex.Message
                });
            }

            var successCount = testResults.Count(r => ((dynamic)r).Status == "Success");
            var errorCount = testResults.Count(r => ((dynamic)r).Status == "Error");

            logger.LogInformation("Database test completed for admin {UserId}. Success: {SuccessCount}, Errors: {ErrorCount}", userId, successCount, errorCount);

            return Results.Ok(new
            {
                Message = "Database connectivity test completed",
                Summary = new
                {
                    TablesSuccessful = successCount,
                    TablesWithErrors = errorCount,
                    TotalTables = testResults.Count
                },
                Results = testResults,
                TestedBy = userId,
                TestedAt = DateTimeOffset.UtcNow,
                Environment = "Development",
                BuildConfiguration = "Debug"
            });
        });

        return endpoints;
    }
#else
    // In Release builds, return empty method to avoid compilation issues
    public static IEndpointRouteBuilder MapDevelopmentEndpoints(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        return endpoints;
    }
#endif
}