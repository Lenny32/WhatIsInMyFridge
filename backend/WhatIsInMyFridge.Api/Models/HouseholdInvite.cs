namespace WhatIsInMyFridge.Api.Models;

public sealed class HouseholdInvite
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HouseholdId { get; set; }
    public string InvitedEmail { get; set; } = string.Empty;
    public Guid InvitedByUserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public InviteStatus Status { get; set; } = InviteStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
}

public enum InviteStatus
{
    Pending,
    Accepted,
    Declined,
    Expired
}
