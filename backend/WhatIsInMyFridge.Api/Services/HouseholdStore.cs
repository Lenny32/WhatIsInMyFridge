using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class HouseholdStore
{
    private readonly AppDbContext _context;
    private readonly ILogger<HouseholdStore> _logger;

    public HouseholdStore(AppDbContext context, ILogger<HouseholdStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Household?> GetByIdAsync(string id)
    {
        return await _context.Households.FindAsync(id);
    }

    public async Task<IReadOnlyCollection<Household>> GetByUserIdAsync(string userId)
    {
        return await _context.Households
            .Where(h => h.MemberIds.Contains(userId))
            .ToArrayAsync();
    }

    public async Task<Household> CreateAsync(Household household)
    {
        _logger.LogInformation("Creating household {HouseholdId} with name {Name}", household.Id, household.Name);
        _context.Households.Add(household);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Household {HouseholdId} created successfully", household.Id);
        return household;
    }

    public async Task<Household?> UpdateAsync(Household household)
    {
        _logger.LogInformation("Updating household {HouseholdId}", household.Id);
        var existing = await _context.Households.FindAsync(household.Id);
        if (existing == null)
        {
            _logger.LogWarning("Update failed: Household {HouseholdId} not found", household.Id);
            return null;
        }

        household.UpdatedAt = DateTimeOffset.UtcNow;
        _context.Entry(existing).CurrentValues.SetValues(household);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Household {HouseholdId} updated successfully", household.Id);
        return household;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        _logger.LogInformation("Deleting household {HouseholdId}", id);
        var household = await _context.Households.FindAsync(id);
        if (household == null)
        {
            _logger.LogWarning("Delete failed: Household {HouseholdId} not found", id);
            return false;
        }

        _context.Households.Remove(household);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Household {HouseholdId} deleted successfully", id);
        return true;
    }
}
