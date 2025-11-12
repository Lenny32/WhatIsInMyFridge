using Microsoft.Extensions.Logging;

namespace WhatIsInMyFridge.Api.Services;

public interface IEmailService
{
    Task SendHouseholdInviteEmailAsync(string recipientEmail, string householdName, string inviteToken, CancellationToken cancellationToken = default);
}

public sealed class MockEmailService : IEmailService
{
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendHouseholdInviteEmailAsync(string recipientEmail, string householdName, string inviteToken, CancellationToken cancellationToken = default)
    {
        // Mock implementation - log the email that would be sent
        _logger.LogInformation(
            "📧 [MOCK EMAIL] Sending household invite email\n" +
            "To: {RecipientEmail}\n" +
            "Subject: You've been invited to join {HouseholdName}\n" +
            "Invite Token: {InviteToken}\n" +
            "Accept Link: /api/households/invites/accept?token={InviteToken}",
            recipientEmail, householdName, inviteToken, inviteToken);

        return Task.CompletedTask;
    }
}
