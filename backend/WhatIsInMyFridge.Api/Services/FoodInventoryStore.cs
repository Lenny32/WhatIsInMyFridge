using Microsoft.EntityFrameworkCore;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class FoodInventoryStore
{
    private readonly AppDbContext _context;

    public FoodInventoryStore(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<FoodItem>> GetItemsAsync(string householdId, StorageLocation? location = null)
    {
        var query = _context.FoodItems.Where(item => item.HouseholdId == householdId);
        
        if (location.HasValue)
        {
            query = query.Where(item => item.Location == location.Value);
        }

        return await query.ToArrayAsync();
    }

    public async Task<IReadOnlyCollection<FoodItem>> GetToBuyListAsync(string householdId)
    {
        return await _context.FoodItems
            .Where(item => item.HouseholdId == householdId 
                && item.Quantity <= item.RestockThreshold)
            .ToArrayAsync();
    }

    public async Task<FoodItem?> GetByIdAsync(string id)
    {
        return await _context.FoodItems.FindAsync(id);
    }

    public async Task<FoodItem> CreateAsync(string householdId, CreateFoodItemRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new FoodItem
        {
            HouseholdId = householdId,
            Name = request.Name.Trim(),
            Location = request.Location,
            Quantity = request.Quantity,
            Unit = request.Unit,
            RestockThreshold = request.RestockThreshold,
            ExpiresAt = request.ExpiresAt,
            Category = request.Category,
            Notes = request.Notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        
        _context.FoodItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<FoodItem?> UpdateAsync(string id, UpdateFoodItemRequest request)
    {
        var existing = await _context.FoodItems.FindAsync(id);
        if (existing == null) return null;

        existing.Name = request.Name is { Length: > 0 } name ? name.Trim() : existing.Name;
        existing.Location = request.Location ?? existing.Location;
        existing.Quantity = request.Quantity ?? existing.Quantity;
        existing.Unit = request.Unit ?? existing.Unit;
        existing.RestockThreshold = request.RestockThreshold ?? existing.RestockThreshold;
        existing.ExpiresAt = request.ExpiresAt ?? existing.ExpiresAt;
        existing.Category = request.Category ?? existing.Category;
        existing.Notes = request.Notes is { Length: > 0 } notes ? notes.Trim() : request.Notes == string.Empty ? null : existing.Notes;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var item = await _context.FoodItems.FindAsync(id);
        if (item == null) return false;

        _context.FoodItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }
}
