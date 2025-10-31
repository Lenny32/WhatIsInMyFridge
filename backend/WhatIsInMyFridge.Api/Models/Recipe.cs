namespace WhatIsInMyFridge.Api.Models;

public sealed class Recipe
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string HouseholdId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Servings { get; set; } = 1;
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    
    // Stored inline within the Recipe document in Cosmos DB
    public List<RecipeIngredient> Ingredients { get; set; } = new();
    
    public List<string> Instructions { get; set; } = new();
    public string? Notes { get; set; }
    public List<string> Photos { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
