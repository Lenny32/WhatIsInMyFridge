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
        group.MapGet("/test-database", async Task<IResult> (HttpContext httpContext, AppDbContext dbContext, UserStore userStore, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Database CRUD test requested by user: {UserId}", userId);
            
            if (string.IsNullOrEmpty(userId))
            {
                logger.LogWarning("Unauthorized database test attempt");
                return Results.Unauthorized();
            }

            // Parse userId as Guid
            if (!Guid.TryParse(userId, out var userGuid))
            {
                logger.LogWarning("Invalid user ID format: {UserId}", userId);
                return Results.BadRequest("Invalid user ID format");
            }

            var currentUser = await userStore.GetByIdAsync(userGuid, cancellationToken);
            if (currentUser == null || !currentUser.IsAdmin)
            {
                logger.LogWarning("Non-admin user {UserId} attempted to access database test endpoint", userId);
                return Results.Forbid();
            }

            logger.LogInformation("Admin {UserId} starting full CRUD database test", userId);

            var testResults = new List<object>();
            Guid? testHouseholdId = currentUser.CurrentHouseholdId;

            if (testHouseholdId == null)
            {
                logger.LogWarning("Admin user {UserId} has no household assigned, creating test household", userId);
                
                try
                {
                    var testHousehold = new Household
                    {
                        Name = "Test Household",
                        OwnerId = userGuid,
                        MemberIds = new List<Guid> { userGuid },
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    
                    dbContext.Households.Add(testHousehold);
                    await dbContext.SaveChangesAsync(cancellationToken);
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
                Guid? testItemId = null;

                try
                {
                    // CREATE
                    var foodItem = new FoodItem
                    {
                        HouseholdId = testHouseholdId.Value,
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
                    await dbContext.SaveChangesAsync(cancellationToken);
                    testItemId = foodItem.Id;
                    operations.Add("Create: Success");

                    // READ - Use FindAsync for partition key-based lookup
                    var readItem = await dbContext.FoodItems.FindAsync(testItemId);
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
                        await dbContext.SaveChangesAsync(cancellationToken);
                        
                        var verifyUpdate = await dbContext.FoodItems.FindAsync(testItemId);
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
                        var itemToDelete = await dbContext.FoodItems.FindAsync(testItemId);
                        if (itemToDelete != null)
                        {
                            dbContext.FoodItems.Remove(itemToDelete);
                            await dbContext.SaveChangesAsync(cancellationToken);
                            
                            var verifyDelete = await dbContext.FoodItems.FindAsync(testItemId);
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
                Guid? testItemId = null;

                try
                {
                    // CREATE
                    var groceryItem = new GroceryItem
                    {
                        HouseholdId = testHouseholdId.Value,
                        Name = "Test Grocery Item",
                        Quantity = 3,
                        Category = FoodCategory.Dairy,
                        Notes = "Test grocery item",
                        IsPurchased = false,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    dbContext.GroceryItems.Add(groceryItem);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    testItemId = groceryItem.Id;
                    operations.Add("Create: Success");

                    // READ - Use FindAsync for partition key-based lookup
                    var readItem = await dbContext.GroceryItems.FindAsync(testItemId);
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
                        await dbContext.SaveChangesAsync(cancellationToken);
                        
                        var verifyUpdate = await dbContext.GroceryItems.FindAsync(testItemId);
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
                        var itemToDelete = await dbContext.GroceryItems.FindAsync(testItemId);
                        if (itemToDelete != null)
                        {
                            dbContext.GroceryItems.Remove(itemToDelete);
                            await dbContext.SaveChangesAsync(cancellationToken);
                            
                            var verifyDelete = await dbContext.GroceryItems.FindAsync(testItemId);
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
                Guid? testItemId = null;

                try
                {
                    // CREATE
                    var recipe = new Recipe
                    {
                        HouseholdId = testHouseholdId.Value,
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
                        Photos = new List<RecipePhoto>(),
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    dbContext.Recipes.Add(recipe);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    testItemId = recipe.Id;
                    operations.Add("Create: Success");

                    // READ - Use FindAsync for partition key-based lookup
                    var readItem = await dbContext.Recipes.FindAsync(testItemId);
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
                        await dbContext.SaveChangesAsync(cancellationToken);
                        
                        var verifyUpdate = await dbContext.Recipes.FindAsync(testItemId);
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
                        var itemToDelete = await dbContext.Recipes.FindAsync(testItemId);
                        if (itemToDelete != null)
                        {
                            dbContext.Recipes.Remove(itemToDelete);
                            await dbContext.SaveChangesAsync(cancellationToken);
                            
                            var verifyDelete = await dbContext.Recipes.FindAsync(testItemId);
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
                Guid? testItemId = null;

                try
                {
                    // CREATE
                    var household = new Household
                    {
                        Name = "Test CRUD Household",
                        OwnerId = userGuid,
                        MemberIds = new List<Guid> { userGuid },
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    dbContext.Households.Add(household);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    testItemId = household.Id;
                    operations.Add("Create: Success");

                    // READ - Use FindAsync for partition key-based lookup
                    var readItem = await dbContext.Households.FindAsync(testItemId);
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
                        await dbContext.SaveChangesAsync(cancellationToken);
                        
                        var verifyUpdate = await dbContext.Households.FindAsync(testItemId);
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
                        var itemToDelete = await dbContext.Households.FindAsync(testItemId);
                        if (itemToDelete != null)
                        {
                            dbContext.Households.Remove(itemToDelete);
                            await dbContext.SaveChangesAsync(cancellationToken);
                            
                            var verifyDelete = await dbContext.Households.FindAsync(testItemId);
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
