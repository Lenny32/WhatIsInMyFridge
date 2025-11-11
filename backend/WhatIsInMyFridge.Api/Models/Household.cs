namespace WhatIsInMyFridge.Api.Models;

public sealed class Household
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
    public List<Guid> MemberIds { get; set; } = new();
    public Guid OwnerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
