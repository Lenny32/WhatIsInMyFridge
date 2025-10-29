using Microsoft.EntityFrameworkCore;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class HouseholdStore
{
    private readonly AppDbContext _context;

    public HouseholdStore(AppDbContext context)
    {
        _context = context;
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
        _context.Households.Add(household);
        await _context.SaveChangesAsync();
        return household;
    }

    public async Task<Household?> UpdateAsync(Household household)
    {
        var existing = await _context.Households.FindAsync(household.Id);
        if (existing == null) return null;

        household.UpdatedAt = DateTimeOffset.UtcNow;
        _context.Entry(existing).CurrentValues.SetValues(household);
        await _context.SaveChangesAsync();
        return household;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var household = await _context.Households.FindAsync(id);
        if (household == null) return false;

        _context.Households.Remove(household);
        await _context.SaveChangesAsync();
        return true;
    }
}
