namespace WhatIsInMyFridge.Api.Models;

public sealed class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsAdmin { get; set; } = false;
    public List<string> HouseholdIds { get; set; } = new();
    public string? CurrentHouseholdId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
