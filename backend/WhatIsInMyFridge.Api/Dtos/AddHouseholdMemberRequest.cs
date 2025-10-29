using System.ComponentModel.DataAnnotations;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class AddHouseholdMemberRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
