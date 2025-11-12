namespace WhatIsInMyFridge.Api.Models;

public sealed class MealPlan
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HouseholdId { get; set; }
    public Guid RecipeId { get; set; }
    public DateTime PlannedDate { get; set; }
    public string? MealName { get; set; } // e.g., "Breakfast", "Lunch", "Dinner", or custom
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
