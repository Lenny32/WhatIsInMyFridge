namespace WhatIsInMyFridge.Api.Models;

public sealed class GroceryItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public FoodCategory Category { get; set; } = FoodCategory.Undefined;
    public string? Notes { get; set; }
    public bool IsPurchased { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
