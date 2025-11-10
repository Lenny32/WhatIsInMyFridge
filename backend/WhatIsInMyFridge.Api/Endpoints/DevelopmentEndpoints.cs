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
using WhatIsInMyFridge.Api.Models;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using WhatIsInMyFridge.Api.Configuration;

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class DevelopmentEndpoints
{
#if DEBUG
    private static string GetDetailedExceptionMessage(Exception ex)
    {
        var messages = new List<string>();
        var currentException = ex;
        var depth = 0;
        
        while (currentException != null && depth < 10)
        {
            var prefix = depth == 0 ? "" : $"Inner Exception {depth}: ";
            messages.Add($"{prefix}{currentException.GetType().Name}: {currentException.Message}");
            
            if (!string.IsNullOrEmpty(currentException.StackTrace))
            {
                var stackLines = currentException.StackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (stackLines.Length > 0)
                {
                    messages.Add($"  at {stackLines[0].Trim()}");
                }
            }
            
            currentException = currentException.InnerException;
            depth++;
        }
        
        return string.Join(" | ", messages);
    }

    public static IEndpointRouteBuilder MapDevelopmentEndpoints(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        // Only register these endpoints in Development mode and DEBUG builds
        if (!environment.IsDevelopment())
        {
            return endpoints;
        }

        var group = endpoints.MapGroup("/api/dev");
        group.RequireAuthorization();

        // Database CRUD testing endpoint - requires admin access
        group.MapGet("/test-database", async Task<IResult> (HttpContext httpContext, AppDbContext dbContext, UserStore userStore, ILogger<Program> logger) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Database CRUD test requested by user: {UserId}", userId);
            
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

            logger.LogInformation("Admin {UserId} starting full CRUD database test", userId);

            var testResults = new List<object>();
            var testHouseholdId = currentUser.CurrentHouseholdId ?? string.Empty;

            if (string.IsNullOrEmpty(testHouseholdId))
            {
                logger.LogWarning("Admin user {UserId} has no household assigned, creating test household", userId);
                
                try
                {
                    var testHousehold = new Household
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = "Test Household",
                        OwnerId = userId,
                        MemberIds = new List<string> { userId },
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    
                    dbContext.Households.Add(testHousehold);
                    await dbContext.SaveChangesAsync();
                    testHouseholdId = testHousehold.Id;
                    
                    logger.LogInformation("Created test household {HouseholdId}", testHouseholdId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create test household");
                    return Results.Problem($"Failed to create test household for CRUD operations. Error: {GetDetailedExceptionMessage(ex)}");
                }
            }

            // Test FoodItems CRUD
            try
            {
                logger.LogDebug("Testing FoodItems CRUD operations");
                var operations = new List<string>();
                string? testItemId = null;

                try
                {
                    // CREATE
                    var foodItem = new FoodItem
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        HouseholdId = testHouseholdId,
                        Name = "Test Food Item",
                        Location = StorageLocation.Fridge,
                        Quantity = 5,
                        Unit = MeasurementUnit.Pieces,
                        RestockThreshold = 2,
                        Category = FoodCategory.Vegetables,
                        Notes = "Test item for CRUD operations",
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    dbContext.FoodItems.Add(foodItem);
                    await dbContext.SaveChangesAsync();
                    testItemId = foodItem.Id;
                    operations.Add("Create: Success");

                    // READ
                    var readItem = await dbContext.FoodItems
                        .Where(item => item.Id == testItemId)
                        .FirstOrDefaultAsync();
                    if (readItem != null && readItem.Name == "Test Food Item")
                    {
                        operations.Add("Read: Success");
                    }
                    else
                    {
                        operations.Add("Read: Failed - Item not found or data mismatch");
                    }

                    // UPDATE
                    if (readItem != null)
                    {
                        readItem.Name = "Updated Test Food Item";
                        readItem.Quantity = 10;
                        readItem.UpdatedAt = DateTimeOffset.UtcNow;
                        await dbContext.SaveChangesAsync();
                        
                        var verifyUpdate = await dbContext.FoodItems
                            .Where(item => item.Id == testItemId)
                            .FirstOrDefaultAsync();
                        if (verifyUpdate != null && verifyUpdate.Name == "Updated Test Food Item" && verifyUpdate.Quantity == 10)
                        {
                            operations.Add("Update: Success");
                        }
                        else
                        {
                            operations.Add("Update: Failed - Changes not persisted");
                        }
                    }

                    // DELETE
                    if (testItemId != null)
                    {
                        var itemToDelete = await dbContext.FoodItems
                            .Where(item => item.Id == testItemId)
                            .FirstOrDefaultAsync();
                        if (itemToDelete != null)
                        {
                            dbContext.FoodItems.Remove(itemToDelete);
                            await dbContext.SaveChangesAsync();
                            
                            var verifyDelete = await dbContext.FoodItems
                                .Where(item => item.Id == testItemId)
                                .FirstOrDefaultAsync();
                            if (verifyDelete == null)
                            {
                                operations.Add("Delete: Success");
                            }
                            else
                            {
                                operations.Add("Delete: Failed - Item still exists");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    operations.Add($"Operation Failed: {GetDetailedExceptionMessage(ex)}");
                    logger.LogError(ex, "Error during FoodItems CRUD operation");
                }

                testResults.Add(new
                {
                    Entity = "FoodItems",
                    Status = operations.All(o => o.Contains("Success")) ? "Success" : "Partial",
                    Operations = operations
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error testing FoodItems CRUD");
                testResults.Add(new
                {
                    Entity = "FoodItems",
                    Status = "Error",
                    Error = GetDetailedExceptionMessage(ex)
                });
            }

            // Test GroceryItems CRUD
            try
            {
                logger.LogDebug("Testing GroceryItems CRUD operations");
                var operations = new List<string>();
                string? testItemId = null;

                try
                {
                    // CREATE
                    var groceryItem = new GroceryItem
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        HouseholdId = testHouseholdId,
                        Name = "Test Grocery Item",
                        Quantity = 3,
                        Category = FoodCategory.Dairy,
                        Notes = "Test grocery item",
                        IsPurchased = false,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    dbContext.GroceryItems.Add(groceryItem);
                    await dbContext.SaveChangesAsync();
                    testItemId = groceryItem.Id;
                    operations.Add("Create: Success");

                    // READ
                    var readItem = await dbContext.GroceryItems
                        .Where(item => item.Id == testItemId)
                        .FirstOrDefaultAsync();
                    if (readItem != null && readItem.Name == "Test Grocery Item")
                    {
                        operations.Add("Read: Success");
                    }
                    else
                    {
                        operations.Add("Read: Failed - Item not found or data mismatch");
                    }

                    // UPDATE
                    if (readItem != null)
                    {
                        readItem.Name = "Updated Grocery Item";
                        readItem.IsPurchased = true;
                        readItem.Quantity = 5;
                        readItem.UpdatedAt = DateTimeOffset.UtcNow;
                        await dbContext.SaveChangesAsync();
                        
                        var verifyUpdate = await dbContext.GroceryItems
                            .Where(item => item.Id == testItemId)
                            .FirstOrDefaultAsync();
                        if (verifyUpdate != null && verifyUpdate.Name == "Updated Grocery Item" && verifyUpdate.IsPurchased)
                        {
                            operations.Add("Update: Success");
                        }
                        else
                        {
                            operations.Add("Update: Failed - Changes not persisted");
                        }
                    }

                    // DELETE
                    if (testItemId != null)
                    {
                        var itemToDelete = await dbContext.GroceryItems
                            .Where(item => item.Id == testItemId)
                            .FirstOrDefaultAsync();
                        if (itemToDelete != null)
                        {
                            dbContext.GroceryItems.Remove(itemToDelete);
                            await dbContext.SaveChangesAsync();
                            
                            var verifyDelete = await dbContext.GroceryItems
                                .Where(item => item.Id == testItemId)
                                .FirstOrDefaultAsync();
                            if (verifyDelete == null)
                            {
                                operations.Add("Delete: Success");
                            }
                            else
                            {
                                operations.Add("Delete: Failed - Item still exists");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    operations.Add($"Operation Failed: {GetDetailedExceptionMessage(ex)}");
                    logger.LogError(ex, "Error during GroceryItems CRUD operation");
                }

                testResults.Add(new
                {
                    Entity = "GroceryItems",
                    Status = operations.All(o => o.Contains("Success")) ? "Success" : "Partial",
                    Operations = operations
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error testing GroceryItems CRUD");
                testResults.Add(new
                {
                    Entity = "GroceryItems",
                    Status = "Error",
                    Error = GetDetailedExceptionMessage(ex)
                });
            }

            // Test Recipes CRUD
            try
            {
                logger.LogDebug("Testing Recipes CRUD operations");
                var operations = new List<string>();
                string? testItemId = null;

                try
                {
                    // CREATE
                    var recipe = new Recipe
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        HouseholdId = testHouseholdId,
                        Name = "Test Recipe",
                        Description = "A test recipe for CRUD operations",
                        Servings = 4,
                        PrepTimeMinutes = 15,
                        CookTimeMinutes = 30,
                        Ingredients = new List<RecipeIngredient>
                        {
                            new RecipeIngredient
                            {
                                Name = "Test Ingredient",
                                Quantity = 2,
                                Unit = MeasurementUnit.Cups
                            }
                        },
                        Instructions = new List<string> { "Step 1: Test", "Step 2: Verify" },
                        Notes = "Test recipe notes",
                        Photos = new List<string>(),
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    dbContext.Recipes.Add(recipe);
                    await dbContext.SaveChangesAsync();
                    testItemId = recipe.Id;
                    operations.Add("Create: Success");

                    // READ
                    var readItem = await dbContext.Recipes
                        .Where(item => item.Id == testItemId)
                        .FirstOrDefaultAsync();
                    if (readItem != null && readItem.Name == "Test Recipe")
                    {
                        operations.Add("Read: Success");
                    }
                    else
                    {
                        operations.Add("Read: Failed - Item not found or data mismatch");
                    }

                    // UPDATE
                    if (readItem != null)
                    {
                        readItem.Name = "Updated Test Recipe";
                        readItem.Servings = 6;
                        readItem.Description = "Updated description";
                        readItem.UpdatedAt = DateTimeOffset.UtcNow;
                        await dbContext.SaveChangesAsync();
                        
                        var verifyUpdate = await dbContext.Recipes
                            .Where(item => item.Id == testItemId)
                            .FirstOrDefaultAsync();
                        if (verifyUpdate != null && verifyUpdate.Name == "Updated Test Recipe" && verifyUpdate.Servings == 6)
                        {
                            operations.Add("Update: Success");
                        }
                        else
                        {
                            operations.Add("Update: Failed - Changes not persisted");
                        }
                    }

                    // DELETE
                    if (testItemId != null)
                    {
                        var itemToDelete = await dbContext.Recipes
                            .Where(item => item.Id == testItemId)
                            .FirstOrDefaultAsync();
                        if (itemToDelete != null)
                        {
                            dbContext.Recipes.Remove(itemToDelete);
                            await dbContext.SaveChangesAsync();
                            
                            var verifyDelete = await dbContext.Recipes
                                .Where(item => item.Id == testItemId)
                                .FirstOrDefaultAsync();
                            if (verifyDelete == null)
                            {
                                operations.Add("Delete: Success");
                            }
                            else
                            {
                                operations.Add("Delete: Failed - Item still exists");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    operations.Add($"Operation Failed: {GetDetailedExceptionMessage(ex)}");
                    logger.LogError(ex, "Error during Recipes CRUD operation");
                }

                testResults.Add(new
                {
                    Entity = "Recipes",
                    Status = operations.All(o => o.Contains("Success")) ? "Success" : "Partial",
                    Operations = operations
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error testing Recipes CRUD");
                testResults.Add(new
                {
                    Entity = "Recipes",
                    Status = "Error",
                    Error = GetDetailedExceptionMessage(ex)
                });
            }

            // Test Households CRUD
            try
            {
                logger.LogDebug("Testing Households CRUD operations");
                var operations = new List<string>();
                string? testItemId = null;

                try
                {
                    // CREATE
                    var household = new Household
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = "Test CRUD Household",
                        OwnerId = userId,
                        MemberIds = new List<string> { userId },
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    dbContext.Households.Add(household);
                    await dbContext.SaveChangesAsync();
                    testItemId = household.Id;
                    operations.Add("Create: Success");

                    // READ
                    var readItem = await dbContext.Households
                        .Where(item => item.Id == testItemId)
                        .FirstOrDefaultAsync();
                    if (readItem != null && readItem.Name == "Test CRUD Household")
                    {
                        operations.Add("Read: Success");
                    }
                    else
                    {
                        operations.Add("Read: Failed - Item not found or data mismatch");
                    }

                    // UPDATE
                    if (readItem != null)
                    {
                        readItem.Name = "Updated CRUD Household";
                        readItem.UpdatedAt = DateTimeOffset.UtcNow;
                        await dbContext.SaveChangesAsync();
                        
                        var verifyUpdate = await dbContext.Households
                            .Where(item => item.Id == testItemId)
                            .FirstOrDefaultAsync();
                        if (verifyUpdate != null && verifyUpdate.Name == "Updated CRUD Household")
                        {
                            operations.Add("Update: Success");
                        }
                        else
                        {
                            operations.Add("Update: Failed - Changes not persisted");
                        }
                    }

                    // DELETE
                    if (testItemId != null)
                    {
                        var itemToDelete = await dbContext.Households
                            .Where(item => item.Id == testItemId)
                            .FirstOrDefaultAsync();
                        if (itemToDelete != null)
                        {
                            dbContext.Households.Remove(itemToDelete);
                            await dbContext.SaveChangesAsync();
                            
                            var verifyDelete = await dbContext.Households
                                .Where(item => item.Id == testItemId)
                                .FirstOrDefaultAsync();
                            if (verifyDelete == null)
                            {
                                operations.Add("Delete: Success");
                            }
                            else
                            {
                                operations.Add("Delete: Failed - Item still exists");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    operations.Add($"Operation Failed: {GetDetailedExceptionMessage(ex)}");
                    logger.LogError(ex, "Error during Households CRUD operation");
                }

                testResults.Add(new
                {
                    Entity = "Households",
                    Status = operations.All(o => o.Contains("Success")) ? "Success" : "Partial",
                    Operations = operations
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error testing Households CRUD");
                testResults.Add(new
                {
                    Entity = "Households",
                    Status = "Error",
                    Error = GetDetailedExceptionMessage(ex)
                });
            }

            var successCount = testResults.Count(r => ((dynamic)r).Status == "Success");
            var partialCount = testResults.Count(r => ((dynamic)r).Status == "Partial");
            var errorCount = testResults.Count(r => ((dynamic)r).Status == "Error");

            logger.LogInformation("Database CRUD test completed for admin {UserId}. Success: {SuccessCount}, Partial: {PartialCount}, Errors: {ErrorCount}", 
                userId, successCount, partialCount, errorCount);

            return Results.Ok(new
            {
                Message = "Database CRUD test completed",
                Summary = new
                {
                    EntitiesSuccessful = successCount,
                    EntitiesPartial = partialCount,
                    EntitiesWithErrors = errorCount,
                    TotalEntities = testResults.Count
                },
                Results = testResults,
                TestedBy = userId,
                TestedAt = DateTimeOffset.UtcNow,
                TestHouseholdId = testHouseholdId,
                Environment = "Development",
                BuildConfiguration = "Debug"
            });
        });

        // Add diagnostics endpoint for both development and staging (not production)
        if (!environment.IsProduction())
        {
            group.MapGet("/cosmos-diagnostics", async (
                CosmosClient cosmosClient, 
                CosmosDbSettings settings, 
                ILogger<Program> logger) =>
            {
                try
                {
                    logger.LogInformation("Running Cosmos DB diagnostics");
                    
                    var diagnostics = new
                    {
                        DatabaseName = settings.DatabaseName,
                        ConnectionStringLength = settings.ConnectionString?.Length ?? 0,
                        ConnectionStringStart = settings.ConnectionString?.Substring(0, Math.Min(50, settings.ConnectionString.Length)) ?? "null",
                        Environment = environment.EnvironmentName
                    };
                    
                    // Test database access
                    try
                    {
                        var database = cosmosClient.GetDatabase(settings.DatabaseName);
                        var response = await database.ReadAsync();
                        
                        // Try to list containers (simplified approach)
                        var containers = new List<string> { "Users", "Households", "FoodItems", "Recipes", "GroceryItems" };
                        var containerStatus = new Dictionary<string, string>();
                        
                        foreach (var containerName in containers)
                        {
                            try
                            {
                                var container = database.GetContainer(containerName);
                                var containerResponse = await container.ReadContainerAsync();
                                containerStatus[containerName] = "Exists";
                            }
                            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                            {
                                containerStatus[containerName] = "Missing";
                            }
                            catch (Exception ex)
                            {
                                containerStatus[containerName] = $"Error: {ex.Message}";
                            }
                        }
                        
                        return Results.Ok(new
                        {
                            Status = "Success",
                            Configuration = diagnostics,
                            Database = new
                            {
                                StatusCode = response.StatusCode,
                                Exists = true,
                                ContainerStatus = containerStatus
                            }
                        });
                    }
                    catch (CosmosException ex)
                    {
                        return Results.Ok(new
                        {
                            Status = "DatabaseError",
                            Configuration = diagnostics,
                            Error = new
                            {
                                StatusCode = ex.StatusCode,
                                Message = ex.Message,
                                ResponseBody = ex.ResponseBody
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Cosmos DB diagnostics failed");
                    return Results.Problem($"Diagnostics failed: {ex.Message}");
                }
            });
        }

        // Only allow full reset in development
        if (environment.IsDevelopment())
        {
            group.MapPost("/reset-data", async (AppDbContext dbContext, ILogger<Program> logger) =>
            {
                logger.LogWarning("Development reset endpoint called - this will delete all data!");
                
                try
                {
                    await dbContext.Database.EnsureDeletedAsync();
                    await dbContext.Database.EnsureCreatedAsync();
                    
                    logger.LogInformation("Database reset completed");
                    return Results.Ok(new { message = "Database reset successfully" });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to reset database");
                    return Results.Problem($"Reset failed: {ex.Message}");
                }
            });
        }

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
