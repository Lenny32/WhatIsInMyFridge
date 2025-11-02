using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace WhatIsInMyFridge.Api.Services;

public sealed class BlobStorageService
{
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly string _containerName;
    private readonly bool _useBlobStorage;
    private readonly string _localPhotosPath;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(
        BlobServiceClient? blobServiceClient, 
        IConfiguration configuration, 
        IWebHostEnvironment environment,
        ILogger<BlobStorageService> logger)
    {
        // Aspire injects BlobServiceClient if available (emulator or cloud)
        // Otherwise, fall back to local file storage
        _blobServiceClient = blobServiceClient;
        _containerName = configuration["BlobStorage:ContainerName"] ?? "blobs";

        // Use /app/data in production (Docker), or ../data locally
        var dataDir = environment.IsProduction() 
            ? "/app/data" 
            : Path.Combine(AppContext.BaseDirectory, "..", "data");
        Directory.CreateDirectory(dataDir);
        _localPhotosPath = Path.Combine(dataDir, "photos");
        Directory.CreateDirectory(_localPhotosPath);

        _useBlobStorage = _blobServiceClient != null;
        _logger = logger;
        
        _logger.LogInformation("BlobStorageService initialized. Using blob storage: {UseBlobStorage}, Local path: {LocalPath}", 
            _useBlobStorage, _localPhotosPath);
    }

    public async Task<string> UploadPhotoAsync(Stream photoStream, string fileName, string contentType)
    {
        _logger.LogInformation("Uploading photo {FileName} with content type {ContentType}", fileName, contentType);
        
        if (_useBlobStorage && _blobServiceClient != null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = containerClient.GetBlobClient(fileName);
            await blobClient.UploadAsync(photoStream, new BlobHttpHeaders { ContentType = contentType });

            _logger.LogInformation("Photo {FileName} uploaded to blob storage: {Uri}", fileName, blobClient.Uri);
            return blobClient.Uri.ToString();
        }
        else
        {
            // Fallback to local file storage
            var filePath = Path.Combine(_localPhotosPath, fileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await photoStream.CopyToAsync(fileStream);
            }
            _logger.LogInformation("Photo {FileName} saved to local storage: {Path}", fileName, filePath);
            return $"/api/photos/{fileName}";
        }
    }

    public async Task DeletePhotoAsync(string photoId)
    {
        _logger.LogInformation("Deleting photo {PhotoId}", photoId);
        
        if (_useBlobStorage && _blobServiceClient != null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            
            // Try common image extensions
            var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            foreach (var ext in extensions)
            {
                var fileName = $"{photoId}{ext}";
                var blobClient = containerClient.GetBlobClient(fileName);
                var deleted = await blobClient.DeleteIfExistsAsync();
                if (deleted)
                {
                    _logger.LogInformation("Deleted photo {PhotoId} from blob storage", fileName);
                }
            }
        }
        else
        {
            // Fallback to local file storage - find and delete any file with this photoId
            var matchingFiles = Directory.GetFiles(_localPhotosPath, $"{photoId}.*");
            foreach (var file in matchingFiles)
            {
                File.Delete(file);
                _logger.LogInformation("Deleted photo from local storage: {Path}", file);
            }
        }
    }

    public async Task<(byte[] fileBytes, string contentType)?> GetPhotoAsync(string fileName)
    {
        _logger.LogDebug("Retrieving photo {FileName}", fileName);
        
        if (_useBlobStorage && _blobServiceClient != null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("Photo {FileName} not found in blob storage", fileName);
                return null;
            }

            var downloadResult = await blobClient.DownloadAsync();
            using var memoryStream = new MemoryStream();
            await downloadResult.Value.Content.CopyToAsync(memoryStream);
            
            _logger.LogDebug("Photo {FileName} retrieved from blob storage", fileName);
            return (memoryStream.ToArray(), downloadResult.Value.ContentType);
        }
        else
        {
            // Fallback to local file storage
            var filePath = Path.Combine(_localPhotosPath, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Photo {FileName} not found in local storage", fileName);
                return null;
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            var fileBytes = await File.ReadAllBytesAsync(filePath);
            _logger.LogDebug("Photo {FileName} retrieved from local storage", fileName);
            return (fileBytes, contentType);
        }
    }

    public bool IsUsingBlobStorage => _useBlobStorage;
}
