using System.ComponentModel.DataAnnotations;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class CreateGroceryItemRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal? Quantity { get; set; }

    public FoodCategory Category { get; set; } = FoodCategory.Undefined;

    [StringLength(500)]
    public string? Notes { get; set; }
}
