using System.ComponentModel.DataAnnotations;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class UpdateMealPlanRequest
{
    public Guid? RecipeId { get; set; }
    
    public DateOnly? PlannedDate { get; set; }

    [StringLength(100)]
    public string? MealName { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
