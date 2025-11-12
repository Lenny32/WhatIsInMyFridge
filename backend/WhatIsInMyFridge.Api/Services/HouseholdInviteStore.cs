using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class HouseholdInviteStore
{
    private readonly AppDbContext _context;
    private readonly ILogger<HouseholdInviteStore> _logger;

    public HouseholdInviteStore(AppDbContext context, ILogger<HouseholdInviteStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HouseholdInvite?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.HouseholdInvites.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<HouseholdInvite?> GetByTokenAsync(string token, CancellationToken cancellationToken)
    {
        return await _context.HouseholdInvites
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);
    }

    public async Task<IReadOnlyCollection<HouseholdInvite>> GetByHouseholdIdAsync(Guid householdId, CancellationToken cancellationToken)
    {
        return await _context.HouseholdInvites
            .Where(i => i.HouseholdId == householdId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<HouseholdInvite>> GetPendingByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await _context.HouseholdInvites
            .Where(i => i.InvitedEmail == email && i.Status == InviteStatus.Pending && i.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task<HouseholdInvite> CreateAsync(HouseholdInvite invite, CancellationToken cancellationToken)
    {
        // Sanitize email for logging to prevent log injection
        var sanitizedEmail = invite.InvitedEmail?.Replace("\n", "").Replace("\r", "").Replace("\t", "");
        _logger.LogInformation("Creating household invite {InviteId} for email {Email}", invite.Id, sanitizedEmail);
        _context.HouseholdInvites.Add(invite);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Household invite {InviteId} created successfully", invite.Id);
        return invite;
    }

    public async Task<HouseholdInvite?> UpdateAsync(HouseholdInvite invite, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating household invite {InviteId}", invite.Id);
        var existing = await _context.HouseholdInvites.FindAsync(new object[] { invite.Id }, cancellationToken);
        if (existing == null)
        {
            _logger.LogWarning("Update failed: Household invite {InviteId} not found", invite.Id);
            return null;
        }

        existing.Status = invite.Status;
        existing.AcceptedAt = invite.AcceptedAt;
        
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Household invite {InviteId} updated successfully", invite.Id);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting household invite {InviteId}", id);
        var invite = await _context.HouseholdInvites.FindAsync(new object[] { id }, cancellationToken);
        if (invite == null)
        {
            _logger.LogWarning("Delete failed: Household invite {InviteId} not found", id);
            return false;
        }

        _context.HouseholdInvites.Remove(invite);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Household invite {InviteId} deleted successfully", id);
        return true;
    }
}
