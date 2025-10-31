using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using WhatIsInMyFridge.Api;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Models;
using WhatIsInMyFridge.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Check if running with Aspire orchestration (AppHost)
var useAspire = builder.Configuration.GetValue<bool>("UseAspire", false);

// Add Aspire service defaults only if configured
if (useAspire)
{
    builder.AddServiceDefaults();
}

// Use /app/data in production (Docker), or ../data locally for photos
var dataDir = builder.Environment.IsProduction() 
    ? "/app/data" 
    : Path.Combine(AppContext.BaseDirectory, "..", "data");
Directory.CreateDirectory(dataDir);

// Configure Cosmos DB
if (useAspire)
{
    // Aspire will inject the connection string
    // Connection string name matches the resource name in AppHost.cs
    builder.AddCosmosDbContext<AppDbContext>("WhatIsInMyFridge");
}
else
{
    // Standalone mode - use direct connection string and database name
    var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb") 
        ?? Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING");
    var databaseName = builder.Configuration["CosmosDb:DatabaseName"] 
        ?? Environment.GetEnvironmentVariable("COSMOS_DATABASE_NAME") 
        ?? "WhatIsInMyFridge";
    
    if (string.IsNullOrEmpty(cosmosConnectionString))
    {
        throw new InvalidOperationException("Cosmos DB connection string is required. Set ConnectionStrings:CosmosDb or COSMOS_CONNECTION_STRING environment variable.");
    }
    
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseCosmos(cosmosConnectionString, databaseName));
}

// Configure Azure Blob Storage
if (useAspire)
{
    // Aspire will inject the connection string
    // Connection name matches the resource name in AppHost.cs
    builder.AddAzureBlobClient("blobs");
}
else
{
    // Standalone mode - use direct connection string
    var blobConnectionString = builder.Configuration.GetConnectionString("BlobStorage") 
        ?? Environment.GetEnvironmentVariable("BLOB_STORAGE_CONNECTION_STRING");
    
    if (string.IsNullOrEmpty(blobConnectionString))
    {
        throw new InvalidOperationException("Blob Storage connection string is required. Set ConnectionStrings:BlobStorage or BLOB_STORAGE_CONNECTION_STRING environment variable.");
    }
    
    builder.Services.AddSingleton(new Azure.Storage.Blobs.BlobServiceClient(blobConnectionString));
}


builder.Services.AddScoped<UserStore>();
builder.Services.AddScoped<HouseholdStore>();
builder.Services.AddScoped<FoodInventoryStore>();
builder.Services.AddScoped<RecipeStore>();
builder.Services.AddScoped<GroceryListStore>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<ApplicationContext>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddSingleton<BlobStorageService>();

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? "WhatIsInMyFridge-SuperSecretKey-ChangeInProduction-MinimumLength32Characters!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

// Add CORS for custom domain
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://fridge.colen.at",
                "https://colen.at"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SchemaFilter<StringEnumSchemaFilter>();
});

var app = builder.Build();

// Note: Cosmos DB database and containers are created via Azure deployment
// EnsureCreatedAsync() is not reliable with Cosmos DB provider
// Database initialization is handled by Azure CLI during deployment

// Exception handling middleware - serialize exceptions in debug mode
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var errorResponse = new ErrorResponse
            {
                Type = ex.GetType().FullName ?? "UnknownException",
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                Source = ex.Source,
                InnerException = ex.InnerException != null
                    ? new ErrorResponse
                    {
                        Type = ex.InnerException.GetType().FullName ?? "UnknownException",
                        Message = ex.InnerException.Message,
                        StackTrace = ex.InnerException.StackTrace,
                        Source = ex.InnerException.Source
                    }
                    : null
            };

            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await context.Response.WriteAsync(json);
        }
    });
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Debug test endpoint - only available in development
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/debug/throw-exception", () =>
    {
        throw new InvalidOperationException("This is a test exception to verify debug mode exception serialization");
    });
}

// Authentication endpoints
app.MapPost("/api/auth/register", async Task<IResult> (RegisterRequest request, AuthenticationService authService, JwtTokenService jwtTokenService) =>
{
    if (!Validate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var result = await authService.RegisterAsync(request.Email, request.Password, request.Name, request.HouseholdName);
    if (result == null)
    {
        return Results.BadRequest(new { error = "User with this email already exists" });
    }

    var token = jwtTokenService.GenerateToken(result.Value.user.Id, result.Value.household.Id);

    return Results.Ok(new
    {
        token,
        user = new
        {
            result.Value.user.Id,
            result.Value.user.Email,
            result.Value.user.Name,
            result.Value.user.HouseholdIds,
            result.Value.user.CurrentHouseholdId
        },
        household = result.Value.household
    });
});

app.MapPost("/api/auth/login", async Task<IResult> (LoginRequest request, AuthenticationService authService, JwtTokenService jwtTokenService) =>
{
    if (!Validate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var user = await authService.AuthenticateAsync(request.Email, request.Password);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var householdId = user.CurrentHouseholdId ?? user.HouseholdIds.FirstOrDefault();
    if (string.IsNullOrEmpty(householdId))
    {
        return Results.BadRequest(new { error = "User is not part of any household" });
    }

    var token = jwtTokenService.GenerateToken(user.Id, householdId);

    return Results.Ok(new
    {
        token,
        user = new
        {
            user.Id,
            user.Email,
            user.Name,
            user.HouseholdIds,
            user.CurrentHouseholdId
        }
    });
});

app.MapPost("/api/auth/logout", () =>
{
    return Results.NoContent();
});

app.MapGet("/api/auth/me", async Task<IResult> (HttpContext httpContext, UserStore userStore, HouseholdStore householdStore) =>
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    var user = await userStore.GetByIdAsync(userId);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var householdId = httpContext.User.FindFirst("householdId")?.Value;
    var household = !string.IsNullOrEmpty(householdId) ? await householdStore.GetByIdAsync(householdId) : null;

    return Results.Ok(new
    {
        user = new
        {
            user.Id,
            user.Email,
            user.Name,
            user.HouseholdIds,
            user.CurrentHouseholdId
        },
        household
    });
}).RequireAuthorization();

// Household management endpoints
app.MapGet("/api/households", async Task<IResult> (HttpContext httpContext, HouseholdStore householdStore) =>
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    var households = await householdStore.GetByUserIdAsync(userId);
    return Results.Ok(households);
}).RequireAuthorization();

app.MapPost("/api/households", async Task<IResult> (CreateHouseholdRequest request, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore) =>
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    if (!Validate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var user = await userStore.GetByIdAsync(userId);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var household = new Household
    {
        Name = request.Name,
        OwnerId = userId,
        MemberIds = new List<string> { userId }
    };

    await householdStore.CreateAsync(household);

    user.HouseholdIds.Add(household.Id);
    await userStore.UpdateAsync(user);

    return Results.Created($"/api/households/{household.Id}", household);
}).RequireAuthorization();

app.MapPost("/api/households/{householdId}/switch", async Task<IResult> (string householdId, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore, JwtTokenService jwtTokenService) =>
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    var household = await householdStore.GetByIdAsync(householdId);
    if (household == null || !household.MemberIds.Contains(userId))
    {
        return Results.NotFound();
    }

    var user = await userStore.GetByIdAsync(userId);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    user.CurrentHouseholdId = householdId;
    await userStore.UpdateAsync(user);

    // Generate a new token with the updated household
    var token = jwtTokenService.GenerateToken(userId, householdId, user.IsAdmin);

    return Results.Ok(new { token, household });
}).RequireAuthorization();

app.MapPost("/api/households/{householdId}/members", async Task<IResult> (string householdId, AddHouseholdMemberRequest request, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore) =>
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    if (!Validate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var household = await householdStore.GetByIdAsync(householdId);
    if (household == null)
    {
        return Results.NotFound();
    }

    if (household.OwnerId != userId)
    {
        return Results.Forbid();
    }

    var newMember = await userStore.GetByEmailAsync(request.Email);
    if (newMember == null)
    {
        return Results.BadRequest(new { error = "User with this email does not exist" });
    }

    if (household.MemberIds.Contains(newMember.Id))
    {
        return Results.BadRequest(new { error = "User is already a member of this household" });
    }

    household.MemberIds.Add(newMember.Id);
    await householdStore.UpdateAsync(household);

    newMember.HouseholdIds.Add(household.Id);
    await userStore.UpdateAsync(newMember);

    return Results.Ok(household);
}).RequireAuthorization();

app.MapDelete("/api/households/{householdId}/members/{memberId}", async Task<IResult> (string householdId, string memberId, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore) =>
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    var household = await householdStore.GetByIdAsync(householdId);
    if (household == null)
    {
        return Results.NotFound();
    }

    if (household.OwnerId != userId && userId != memberId)
    {
        return Results.Forbid();
    }

    if (!household.MemberIds.Contains(memberId))
    {
        return Results.NotFound();
    }

    if (household.OwnerId == memberId)
    {
        return Results.BadRequest(new { error = "Cannot remove the owner from the household" });
    }

    household.MemberIds.Remove(memberId);
    await householdStore.UpdateAsync(household);

    var member = await userStore.GetByIdAsync(memberId);
    if (member != null)
    {
        member.HouseholdIds.Remove(household.Id);
        if (member.CurrentHouseholdId == household.Id)
        {
            member.CurrentHouseholdId = member.HouseholdIds.FirstOrDefault();
        }
        await userStore.UpdateAsync(member);
    }

    return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/api/items", async Task<IResult> (string? location, HttpContext httpContext, FoodInventoryStore store) =>
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
}).RequireAuthorization();

app.MapGet("/api/items/to-buy", async Task<IResult> (HttpContext httpContext, FoodInventoryStore store) =>
{
    var householdId = httpContext.User.FindFirst("householdId")?.Value;
    if (string.IsNullOrEmpty(householdId))
    {
        return Results.Unauthorized();
    }

    var items = await store.GetToBuyListAsync(householdId);
    return Results.Ok(items);
}).RequireAuthorization();

app.MapGet("/api/items/{id}", async Task<IResult> (string id, FoodInventoryStore store) =>
{
    var item = await store.GetByIdAsync(id);
    return item is null ? Results.NotFound() : Results.Ok(item);
}).RequireAuthorization();

app.MapPost("/api/items", async Task<IResult> (CreateFoodItemRequest request, HttpContext httpContext, FoodInventoryStore store) =>
{
    var householdId = httpContext.User.FindFirst("householdId")?.Value;
    if (string.IsNullOrEmpty(householdId))
    {
        return Results.Unauthorized();
    }

    if (!Validate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var item = await store.CreateAsync(householdId, request);
    return Results.Created($"/api/items/{item.Id}", item);
}).RequireAuthorization();

app.MapPatch("/api/items/{id}", async Task<IResult> (string id, UpdateFoodItemRequest request, FoodInventoryStore store) =>
{
    if (!Validate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var updated = await store.UpdateAsync(id, request);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
}).RequireAuthorization();

app.MapDelete("/api/items/{id}", async Task<IResult> (string id, FoodInventoryStore store) =>
{
    var deleted = await store.DeleteAsync(id);
    return deleted ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

// Recipe endpoints
app.MapGet("/api/recipes", async Task<IResult> (HttpContext httpContext, RecipeStore store) =>
{
    var householdId = httpContext.User.FindFirst("householdId")?.Value;
    if (string.IsNullOrEmpty(householdId))
    {
        return Results.Unauthorized();
    }

    var recipes = await store.GetRecipesAsync(householdId);
    return Results.Ok(recipes);
}).RequireAuthorization();

app.MapGet("/api/recipes/{id}", async Task<IResult> (string id, HttpContext httpContext, RecipeStore store) =>
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
}).RequireAuthorization();

app.MapPost("/api/recipes", async Task<IResult> (CreateRecipeRequest request, HttpContext httpContext, RecipeStore store) =>
{
    var householdId = httpContext.User.FindFirst("householdId")?.Value;
    if (string.IsNullOrEmpty(householdId))
    {
        return Results.Unauthorized();
    }

    if (!Validate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var recipe = await store.CreateAsync(householdId, request);
    return Results.Created($"/api/recipes/{recipe.Id}", recipe);
}).RequireAuthorization();

app.MapPatch("/api/recipes/{id}", async Task<IResult> (string id, UpdateRecipeRequest request, HttpContext httpContext, RecipeStore store) =>
{
    var householdId = httpContext.User.FindFirst("householdId")?.Value;
    if (string.IsNullOrEmpty(householdId))
    {
        return Results.Unauthorized();
    }

    if (!Validate(request, out var errors))
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
}).RequireAuthorization();

app.MapDelete("/api/recipes/{id}", async Task<IResult> (string id, HttpContext httpContext, RecipeStore store) =>
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
}).RequireAuthorization();

// Ingredient autocomplete endpoint
app.MapGet("/api/ingredients/suggestions", async Task<IResult> (string? query, HttpContext httpContext, RecipeStore store) =>
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
}).RequireAuthorization();

// Photo upload endpoint
app.MapPost("/api/recipes/{id}/photos", async Task<IResult> (string id, IFormFile file, HttpContext httpContext, RecipeStore store, BlobStorageService blobStorage) =>
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

    // Validate file
    if (file.Length == 0)
    {
        return Results.BadRequest(new { error = "Empty file" });
    }

    if (file.Length > 5 * 1024 * 1024) // 5MB limit
    {
        return Results.BadRequest(new { error = "File too large. Maximum size is 5MB" });
    }

    var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
    if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
    {
        return Results.BadRequest(new { error = "Invalid file type. Only JPEG, PNG, and WebP images are allowed" });
    }

    // Generate unique filename
    var extension = Path.GetExtension(file.FileName);
    var photoId = Guid.NewGuid().ToString("N");
    var fileName = $"{photoId}{extension}";

    // Upload to blob storage or local filesystem
    using (var stream = file.OpenReadStream())
    {
        await blobStorage.UploadPhotoAsync(stream, fileName, file.ContentType);
    }

    // Update recipe
    recipe.Photos.Add(photoId);
    recipe.UpdatedAt = DateTimeOffset.UtcNow;
    await store.UpdatePhotosAsync(id, recipe.Photos);

    return Results.Ok(new { photoId, url = $"/api/photos/{photoId}{extension}" });
}).RequireAuthorization().DisableAntiforgery();

// Delete photo endpoint
app.MapDelete("/api/recipes/{id}/photos/{photoId}", async Task<IResult> (string id, string photoId, HttpContext httpContext, RecipeStore store, BlobStorageService blobStorage) =>
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

    // Delete photo from blob storage or filesystem
    await blobStorage.DeletePhotoAsync(photoId);

    // Update recipe
    recipe.Photos.Remove(photoId);
    recipe.UpdatedAt = DateTimeOffset.UtcNow;
    await store.UpdatePhotosAsync(id, recipe.Photos);

    return Results.NoContent();
}).RequireAuthorization();

// Serve photo files
app.MapGet("/api/photos/{fileName}", async Task<IResult> (string fileName, BlobStorageService blobStorage) =>
{
    var photoData = await blobStorage.GetPhotoAsync(fileName);
    if (photoData == null)
    {
        return Results.NotFound();
    }

    return Results.File(photoData.Value.fileBytes, photoData.Value.contentType);
});

// Grocery list endpoints
app.MapGet("/api/grocery", async Task<IResult> (HttpContext httpContext, GroceryListStore store) =>
{
    var householdId = httpContext.User.FindFirst("householdId")?.Value;
    if (string.IsNullOrEmpty(householdId))
    {
        return Results.Unauthorized();
    }

    var items = await store.GetAllAsync(householdId);
    return Results.Ok(items);
}).RequireAuthorization();

app.MapPost("/api/grocery", async Task<IResult> (CreateGroceryItemRequest request, HttpContext httpContext, GroceryListStore store) =>
{
    var householdId = httpContext.User.FindFirst("householdId")?.Value;
    if (string.IsNullOrEmpty(householdId))
    {
        return Results.Unauthorized();
    }

    if (!Validate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var item = await store.CreateAsync(householdId, request);
    return Results.Created($"/api/grocery/{item.Id}", item);
}).RequireAuthorization();

app.MapPatch("/api/grocery/{id}", async Task<IResult> (string id, UpdateGroceryItemRequest request, HttpContext httpContext, GroceryListStore store) =>
{
    var householdId = httpContext.User.FindFirst("householdId")?.Value;
    if (string.IsNullOrEmpty(householdId))
    {
        return Results.Unauthorized();
    }

    if (!Validate(request, out var errors))
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
}).RequireAuthorization();

app.MapDelete("/api/grocery/{id}", async Task<IResult> (string id, HttpContext httpContext, GroceryListStore store) =>
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
}).RequireAuthorization();

app.MapDelete("/api/grocery/purchased", async Task<IResult> (HttpContext httpContext, GroceryListStore store) =>
{
    var householdId = httpContext.User.FindFirst("householdId")?.Value;
    if (string.IsNullOrEmpty(householdId))
    {
        return Results.Unauthorized();
    }

    await store.ClearPurchasedAsync(householdId);
    return Results.NoContent();
}).RequireAuthorization();

// Admin endpoints
app.MapGet("/api/admin/users", async Task<IResult> (HttpContext httpContext, UserStore userStore) =>
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    var currentUser = await userStore.GetByIdAsync(userId);
    if (currentUser == null || !currentUser.IsAdmin)
    {
        return Results.Forbid();
    }

    var users = await userStore.GetAllUsersAsync();
    
    // Return user list without password hashes
    var userList = users.Select(u => new
    {
        u.Id,
        u.Email,
        u.Name,
        u.IsAdmin,
        u.CreatedAt,
        u.UpdatedAt
    });

    return Results.Ok(userList);
}).RequireAuthorization();

app.MapPost("/api/admin/users/{id}/reset-password", async Task<IResult> (string id, ResetPasswordRequest request, HttpContext httpContext, UserStore userStore, PasswordHasher passwordHasher) =>
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    var currentUser = await userStore.GetByIdAsync(userId);
    if (currentUser == null || !currentUser.IsAdmin)
    {
        return Results.Forbid();
    }

    if (!Validate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var targetUser = await userStore.GetByIdAsync(id);
    if (targetUser == null)
    {
        return Results.NotFound();
    }

    targetUser.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
    await userStore.UpdateAsync(targetUser);

    return Results.Ok(new { message = "Password reset successfully" });
}).RequireAuthorization();

app.Run();

static bool Validate(object model, out Dictionary<string, string[]> errors)
{
    var validationContext = new ValidationContext(model);
    var validationResults = new List<ValidationResult>();

    var isValid = Validator.TryValidateObject(model, validationContext, validationResults, true);
    errors = validationResults
        .GroupBy(result => result.MemberNames.FirstOrDefault() ?? string.Empty)
        .ToDictionary(
            group => group.Key,
            group => group.Select(result => result.ErrorMessage ?? "Invalid value").ToArray()
        );

    return isValid;
}
