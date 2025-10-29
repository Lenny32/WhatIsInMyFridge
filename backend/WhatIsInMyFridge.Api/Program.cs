using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Models;
using WhatIsInMyFridge.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var dataDir = Path.Combine(AppContext.BaseDirectory, "..", "data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "whatsinmyfridge.db");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<UserStore>();
builder.Services.AddScoped<HouseholdStore>();
builder.Services.AddScoped<FoodInventoryStore>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<ApplicationContext>();
builder.Services.AddScoped<JwtTokenService>();

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
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

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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
    if (user != null)
    {
        user.CurrentHouseholdId = householdId;
        await userStore.UpdateAsync(user);
    }

    // Generate a new token with the updated household
    var token = jwtTokenService.GenerateToken(userId, householdId);

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
