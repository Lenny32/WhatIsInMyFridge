using System.Text.Json;
using System.Text.Json.Serialization;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class FoodInventoryStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FoodInventoryStore(string filePath)
    {
        _filePath = filePath;
    }

    private async Task EnsureFileAsync()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_filePath))
        {
            await File.WriteAllTextAsync(_filePath, "[]");
        }
    }

    private async Task<List<FoodItem>> ReadItemsAsync()
    {
        await EnsureFileAsync();
        await using var stream = File.OpenRead(_filePath);
        var items = await JsonSerializer.DeserializeAsync<List<FoodItem>>(stream, _jsonOptions);
        return items ?? new List<FoodItem>();
    }

    private async Task WriteItemsAsync(IEnumerable<FoodItem> items)
    {
        await EnsureFileAsync();
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, items, _jsonOptions);
    }

    public async Task<IReadOnlyCollection<FoodItem>> GetItemsAsync(StorageLocation? location = null)
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadItemsAsync();
            if (location is null) return items;
            return items.Where(item => item.Location == location).ToArray();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyCollection<FoodItem>> GetToBuyListAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadItemsAsync();
            return items
                .Where(item => item.TrackShoppingList && item.Quantity <= item.RestockThreshold)
                .ToArray();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<FoodItem?> GetByIdAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadItemsAsync();
            return items.FirstOrDefault(item => item.Id == id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<FoodItem> CreateAsync(CreateFoodItemRequest request)
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadItemsAsync();
            var now = DateTimeOffset.UtcNow;
            var item = new FoodItem
            {
                Name = request.Name.Trim(),
                Location = request.Location,
                Quantity = request.Quantity,
                Unit = request.Unit.Trim(),
                RestockThreshold = request.RestockThreshold,
                ExpiresAt = request.ExpiresAt,
                Category = request.Category?.Trim(),
                TrackShoppingList = request.TrackShoppingList,
                Notes = request.Notes?.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };
            items.Add(item);
            await WriteItemsAsync(items);
            return item;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<FoodItem?> UpdateAsync(string id, UpdateFoodItemRequest request)
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadItemsAsync();
            var index = items.FindIndex(item => item.Id == id);
            if (index < 0) return null;

            var existing = items[index];
            existing.Name = request.Name is { Length: > 0 } name ? name.Trim() : existing.Name;
            existing.Location = request.Location ?? existing.Location;
            existing.Quantity = request.Quantity ?? existing.Quantity;
            existing.Unit = request.Unit is { Length: > 0 } unit ? unit.Trim() : existing.Unit;
            existing.RestockThreshold = request.RestockThreshold ?? existing.RestockThreshold;
            existing.ExpiresAt = request.ExpiresAt ?? existing.ExpiresAt;
            existing.Category = request.Category is { Length: > 0 } category ? category.Trim() : request.Category == string.Empty ? null : existing.Category;
            existing.TrackShoppingList = request.TrackShoppingList ?? existing.TrackShoppingList;
            existing.Notes = request.Notes is { Length: > 0 } notes ? notes.Trim() : request.Notes == string.Empty ? null : existing.Notes;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            items[index] = existing;
            await WriteItemsAsync(items);
            return existing;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadItemsAsync();
            var removed = items.RemoveAll(item => item.Id == id);
            if (removed == 0) return false;
            await WriteItemsAsync(items);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }
}
