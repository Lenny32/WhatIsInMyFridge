namespace WhatIsInMyFridge.Api.Models;

public sealed class RecipePhoto
{
    /// <summary>
    /// The stored filename in format: UserId_yyyyMMddHHmmss.{ext}
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// The original filename uploaded by the user
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;
    
    /// <summary>
    /// When the photo was uploaded
    /// </summary>
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
