using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class GroceryListStore
{
    private readonly AppDbContext _context;
    private readonly ILogger<GroceryListStore> _logger;

    public GroceryListStore(AppDbContext context, ILogger<GroceryListStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<GroceryItem>> GetAllAsync(string householdId)
    {
        _logger.LogDebug("Retrieving grocery items for household {HouseholdId}", householdId);
        var items = await _context.GroceryItems
            .Where(item => item.HouseholdId == householdId)
            .ToArrayAsync();
        
        _logger.LogDebug("Retrieved {Count} grocery items for household {HouseholdId}", items.Length, householdId);
        return items
            .OrderBy(item => item.IsPurchased)
            .ThenByDescending(item => item.CreatedAt)
            .ToArray();
    }

    public async Task<GroceryItem?> GetByIdAsync(string id)
    {
        // For Cosmos DB, use Where instead of FindAsync when partition key != Id
        return await _context.GroceryItems
            .Where(item => item.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<GroceryItem> CreateAsync(string householdId, CreateGroceryItemRequest request)
    {
        _logger.LogInformation("Creating grocery item {Name} for household {HouseholdId}", request.Name, householdId);
        var now = DateTimeOffset.UtcNow;
        var item = new GroceryItem
        {
            Id = Guid.NewGuid().ToString(),
            HouseholdId = householdId,
            Name = request.Name.Trim(),
            Quantity = request.Quantity,
            Category = request.Category,
            Notes = request.Notes?.Trim(),
            IsPurchased = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.GroceryItems.Add(item);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Grocery item {ItemId} created successfully", item.Id);
        return item;
    }

    public async Task<GroceryItem?> UpdateAsync(string id, UpdateGroceryItemRequest request)
    {
        _logger.LogInformation("Updating grocery item {ItemId}", id);
        var existing = await _context.GroceryItems
            .Where(item => item.Id == id)
            .FirstOrDefaultAsync();
        if (existing == null)
        {
            _logger.LogWarning("Update failed: Grocery item {ItemId} not found", id);
            return null;
        }

        existing.Name = request.Name is { Length: > 0 } name ? name.Trim() : existing.Name;
        existing.Quantity = request.Quantity ?? existing.Quantity;
        existing.Category = request.Category ?? existing.Category;
        existing.Notes = request.Notes is { Length: > 0 } notes ? notes.Trim() : request.Notes == string.Empty ? null : existing.Notes;
        existing.IsPurchased = request.IsPurchased ?? existing.IsPurchased;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Grocery item {ItemId} updated successfully", id);
        return existing;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        _logger.LogInformation("Deleting grocery item {ItemId}", id);
        var item = await _context.GroceryItems
            .Where(item => item.Id == id)
            .FirstOrDefaultAsync();
        if (item == null)
        {
            _logger.LogWarning("Delete failed: Grocery item {ItemId} not found", id);
            return false;
        }

        _context.GroceryItems.Remove(item);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Grocery item {ItemId} deleted successfully", id);
        return true;
    }

    public async Task<int> ClearPurchasedAsync(string householdId)
    {
        _logger.LogInformation("Clearing purchased items for household {HouseholdId}", householdId);
        var purchasedItems = await _context.GroceryItems
            .Where(item => item.HouseholdId == householdId && item.IsPurchased)
            .ToArrayAsync();

        _context.GroceryItems.RemoveRange(purchasedItems);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Cleared {Count} purchased items for household {HouseholdId}", purchasedItems.Length, householdId);
        return purchasedItems.Length;
    }
}
