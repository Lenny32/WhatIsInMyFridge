namespace WhatIsInMyFridge.Api.Models;

public sealed class RecipeIngredient
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RecipeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Pieces;
    public string? Notes { get; set; }
}
