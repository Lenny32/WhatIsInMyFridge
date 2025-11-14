using System.ComponentModel.DataAnnotations;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class CreateMealPlanRequest
{
    [Required]
    public Guid RecipeId { get; set; }

    [Required]
    public DateTime PlannedDate { get; set; }

    [StringLength(100)]
    public string? MealName { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
