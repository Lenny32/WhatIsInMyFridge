using System.ComponentModel.DataAnnotations;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class UpdateRecipeRequest
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(1, 100)]
    public int? Servings { get; set; }

    [Range(0, 1440)]
    public int? PrepTimeMinutes { get; set; }

    [Range(0, 1440)]
    public int? CookTimeMinutes { get; set; }

    public List<CreateRecipeIngredientRequest>? Ingredients { get; set; }

    public List<string>? Instructions { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
