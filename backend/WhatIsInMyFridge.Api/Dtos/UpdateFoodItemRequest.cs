using System.ComponentModel.DataAnnotations;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Dtos;

public sealed class UpdateFoodItemRequest
{
    [StringLength(100)]
    public string? Name { get; set; }

    public StorageLocation? Location { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Quantity { get; set; }

    [StringLength(20)]
    public string? Unit { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? RestockThreshold { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    [StringLength(50)]
    public string? Category { get; set; }

    public bool? TrackShoppingList { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
