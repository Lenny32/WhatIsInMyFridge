using System.ComponentModel.DataAnnotations;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class CreateRecipeRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(1, 100)]
    public int Servings { get; set; } = 1;

    [Range(0, 1440)]
    public int PrepTimeMinutes { get; set; }

    [Range(0, 1440)]
    public int CookTimeMinutes { get; set; }

    [Required]
    public List<CreateRecipeIngredientRequest> Ingredients { get; set; } = new();

    [Required]
    public List<string> Instructions { get; set; } = new();

    [StringLength(1000)]
    public string? Notes { get; set; }
}
