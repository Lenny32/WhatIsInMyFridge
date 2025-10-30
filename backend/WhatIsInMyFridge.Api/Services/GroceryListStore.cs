using Microsoft.EntityFrameworkCore;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class GroceryListStore
{
    private readonly AppDbContext _context;

    public GroceryListStore(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<GroceryItem>> GetAllAsync(string householdId)
    {
        return await _context.GroceryItems
            .Where(item => item.HouseholdId == householdId)
            .OrderBy(item => item.IsPurchased)
            .ThenByDescending(item => item.CreatedAt)
            .ToArrayAsync();
    }

    public async Task<GroceryItem?> GetByIdAsync(string id)
    {
        return await _context.GroceryItems.FindAsync(id);
    }

    public async Task<GroceryItem> CreateAsync(string householdId, CreateGroceryItemRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new GroceryItem
        {
            HouseholdId = householdId,
            Name = request.Name.Trim(),
            Quantity = request.Quantity,
            Unit = request.Unit,
            Category = request.Category,
            Notes = request.Notes?.Trim(),
            IsPurchased = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.GroceryItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<GroceryItem?> UpdateAsync(string id, UpdateGroceryItemRequest request)
    {
        var existing = await _context.GroceryItems.FindAsync(id);
        if (existing == null) return null;

        existing.Name = request.Name is { Length: > 0 } name ? name.Trim() : existing.Name;
        existing.Quantity = request.Quantity ?? existing.Quantity;
        existing.Unit = request.Unit ?? existing.Unit;
        existing.Category = request.Category ?? existing.Category;
        existing.Notes = request.Notes is { Length: > 0 } notes ? notes.Trim() : request.Notes == string.Empty ? null : existing.Notes;
        existing.IsPurchased = request.IsPurchased ?? existing.IsPurchased;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var item = await _context.GroceryItems.FindAsync(id);
        if (item == null) return false;

        _context.GroceryItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> ClearPurchasedAsync(string householdId)
    {
        var purchasedItems = await _context.GroceryItems
            .Where(item => item.HouseholdId == householdId && item.IsPurchased)
            .ToArrayAsync();

        _context.GroceryItems.RemoveRange(purchasedItems);
        await _context.SaveChangesAsync();
        return purchasedItems.Length;
    }
}
