using System.ComponentModel.DataAnnotations;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class ResetPasswordRequest
{
    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}
