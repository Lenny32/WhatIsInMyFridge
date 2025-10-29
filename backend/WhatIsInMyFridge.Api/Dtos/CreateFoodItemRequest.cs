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
    [StringLength(20)]
    public string Unit { get; set; } = "pcs";

    [Range(0, double.MaxValue)]
    public decimal RestockThreshold { get; set; } = 1;

    public DateTimeOffset? ExpiresAt { get; set; }

    [StringLength(50)]
    public string? Category { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
