using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class FoodInventoryStore
{
    private readonly AppDbContext _context;
    private readonly ILogger<FoodInventoryStore> _logger;

    public FoodInventoryStore(AppDbContext context, ILogger<FoodInventoryStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<FoodItem>> GetItemsAsync(Guid householdId, StorageLocation? location = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving food items for household {HouseholdId}, location {Location}", householdId, location?.ToString() ?? "all");
        var query = _context.FoodItems.Where(item => item.HouseholdId == householdId);
        
        if (location.HasValue)
        {
            query = query.Where(item => item.Location == location.Value);
        }

        var items = await query.ToArrayAsync(cancellationToken);
        _logger.LogDebug("Retrieved {Count} food items for household {HouseholdId}", items.Length, householdId);
        return items;
    }

    public async Task<IReadOnlyCollection<FoodItem>> GetToBuyListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving to-buy list for household {HouseholdId}", householdId);
        
        // Load all items for the household first, then filter in memory
        // This avoids the Cosmos DB field-to-field decimal comparison issue
        var allItems = await _context.FoodItems
            .Where(item => item.HouseholdId == householdId)
            .ToArrayAsync(cancellationToken);
            
        var items = allItems
            .Where(item => item.Quantity <= item.RestockThreshold)
            .ToArray();
            
        _logger.LogDebug("Retrieved {Count} items to buy for household {HouseholdId}", items.Length, householdId);
        return items;
    }

    public async Task<FoodItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.FoodItems.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<FoodItem> CreateAsync(Guid householdId, CreateFoodItemRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating food item {Name} for household {HouseholdId}", request.Name, householdId);
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
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Food item {ItemId} created successfully", item.Id);
        return item;
    }

    public async Task<FoodItem?> UpdateAsync(Guid id, UpdateFoodItemRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating food item {ItemId}", id);
        var existing = await _context.FoodItems.FindAsync(new object[] { id }, cancellationToken);
        if (existing == null)
        {
            _logger.LogWarning("Update failed: Food item {ItemId} not found", id);
            return null;
        }

        existing.Name = request.Name is { Length: > 0 } name ? name.Trim() : existing.Name;
        existing.Location = request.Location ?? existing.Location;
        existing.Quantity = request.Quantity ?? existing.Quantity;
        existing.Unit = request.Unit ?? existing.Unit;
        existing.RestockThreshold = request.RestockThreshold ?? existing.RestockThreshold;
        existing.ExpiresAt = request.ExpiresAt ?? existing.ExpiresAt;
        existing.Category = request.Category ?? existing.Category;
        existing.Notes = request.Notes is { Length: > 0 } notes ? notes.Trim() : request.Notes == string.Empty ? null : existing.Notes;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Food item {ItemId} updated successfully", id);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting food item {ItemId}", id);
        var item = await _context.FoodItems.FindAsync(new object[] { id }, cancellationToken);
        if (item == null)
        {
            _logger.LogWarning("Delete failed: Food item {ItemId} not found", id);
            return false;
        }

        _context.FoodItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Food item {ItemId} deleted successfully", id);
        return true;
    }
}
