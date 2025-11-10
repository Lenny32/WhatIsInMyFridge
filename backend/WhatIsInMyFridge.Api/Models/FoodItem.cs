namespace WhatIsInMyFridge.Api.Models;

public sealed class FoodItem
{
    public string Id { get; set; } = string.Empty;
    public string HouseholdId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public StorageLocation Location { get; set; } = StorageLocation.Fridge;
    public decimal Quantity { get; set; }
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Pieces;
    public decimal RestockThreshold { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public FoodCategory Category { get; set; } = FoodCategory.Undefined;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
