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

    public async Task<Household?> GetByIdAsync(Guid id)
    {
        return await _context.Households.FindAsync(id);
    }

    public async Task<IReadOnlyCollection<Household>> GetByUserIdAsync(Guid userId)
    {
        // Alternative approach: Use User.HouseholdIds to avoid ARRAY_CONTAINS query on Household.MemberIds
        // This bypasses the Cosmos DB emulator indexing issue with array operations
        var user = await _context.Users.FindAsync(userId);
        if (user?.HouseholdIds == null || user.HouseholdIds.Count == 0)
        {
            return Array.Empty<Household>();
        }

        // Query households by their IDs using efficient primary key lookups
        var households = new List<Household>();
        foreach (var householdId in user.HouseholdIds)
        {
            var household = await _context.Households.FindAsync(householdId);
            if (household != null)
            {
                households.Add(household);
            }
        }

        return households.AsReadOnly();
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

        // Update individual properties to avoid partition key conflicts
        existing.Name = household.Name;
        existing.MemberIds = household.MemberIds;
        existing.OwnerId = household.OwnerId;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        
        await _context.SaveChangesAsync();
        _logger.LogInformation("Household {HouseholdId} updated successfully", household.Id);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
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
