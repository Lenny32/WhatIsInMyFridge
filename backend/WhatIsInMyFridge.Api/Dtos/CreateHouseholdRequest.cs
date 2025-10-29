using System.ComponentModel.DataAnnotations;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class CreateHouseholdRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}
