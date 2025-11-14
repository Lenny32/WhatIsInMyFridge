using System.ComponentModel.DataAnnotations;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class CreateHouseholdInviteRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public sealed class AcceptHouseholdInviteRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
