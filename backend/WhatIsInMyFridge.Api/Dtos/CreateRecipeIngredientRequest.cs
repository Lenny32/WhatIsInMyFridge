using System.ComponentModel.DataAnnotations;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class CreateRecipeIngredientRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(0.001, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Required]
    public MeasurementUnit Unit { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
