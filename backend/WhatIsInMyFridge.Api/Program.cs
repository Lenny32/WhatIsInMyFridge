using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Models;
using WhatIsInMyFridge.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var dataFile = Path.Combine(AppContext.BaseDirectory, "..", "data", "items.json");
builder.Services.AddSingleton(new FoodInventoryStore(Path.GetFullPath(dataFile)));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/items", async Task<IResult> (string? location, FoodInventoryStore store) =>
{
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

    var items = await store.GetItemsAsync(parsedLocation);
    return Results.Ok(items);
});

app.MapGet("/api/items/to-buy", async Task<IResult> (FoodInventoryStore store) =>
{
    var items = await store.GetToBuyListAsync();
    return Results.Ok(items);
});

app.MapGet("/api/items/{id}", async Task<IResult> (string id, FoodInventoryStore store) =>
{
    var item = await store.GetByIdAsync(id);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapPost("/api/items", async Task<IResult> (CreateFoodItemRequest request, FoodInventoryStore store) =>
{
    if (!Validate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var item = await store.CreateAsync(request);
    return Results.Created($"/api/items/{item.Id}", item);
});

app.MapPatch("/api/items/{id}", async Task<IResult> (string id, UpdateFoodItemRequest request, FoodInventoryStore store) =>
{
    if (!Validate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var updated = await store.UpdateAsync(id, request);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
});

app.MapDelete("/api/items/{id}", async Task<IResult> (string id, FoodInventoryStore store) =>
{
    var deleted = await store.DeleteAsync(id);
    return deleted ? Results.NoContent() : Results.NotFound();
});

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
