using System.ComponentModel.DataAnnotations;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class CreateFoodItemRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public StorageLocation Location { get; set; } = StorageLocation.Fridge;

    [Range(0, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Required]
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Pieces;

    [Range(0, double.MaxValue)]
    public decimal RestockThreshold { get; set; } = 1;

    public DateTimeOffset? ExpiresAt { get; set; }

    public FoodCategory Category { get; set; } = FoodCategory.Undefined;

    [StringLength(500)]
    public string? Notes { get; set; }
}
