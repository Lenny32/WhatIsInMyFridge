namespace WhatIsInMyFridge.Api.Models;

public sealed class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsAdmin { get; set; } = false;
    public List<Guid> HouseholdIds { get; set; } = new();
    public Guid? CurrentHouseholdId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
