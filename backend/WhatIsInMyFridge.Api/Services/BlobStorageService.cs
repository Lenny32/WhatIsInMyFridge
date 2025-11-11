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
        _useBlobStorage = _blobServiceClient != null;
        _logger = logger;

        // Only set up local storage if blob storage is not available
        if (!_useBlobStorage)
        {
            // Get the local storage path from configuration with a sensible default
            // This allows different paths for different environments via appsettings.json
            var localStoragePath = configuration["BlobStorage:LocalPath"];
            
            if (string.IsNullOrEmpty(localStoragePath))
            {
                // Use ContentRootPath (the app's root directory) as the base
                // This is more reliable than AppContext.BaseDirectory
                localStoragePath = Path.Combine(environment.ContentRootPath, "App_Data", "photos");
            }
            
            try
            {
                _localPhotosPath = Path.GetFullPath(localStoragePath);
                Directory.CreateDirectory(_localPhotosPath);
                _logger.LogInformation("Local file storage initialized at: {LocalPath}", _localPhotosPath);
            }
            catch (Exception ex)
            {
                // If we can't create the directory, fall back to temp directory
                _logger.LogWarning(ex, "Failed to create local storage directory at {LocalPath}, using temp directory", localStoragePath);
                _localPhotosPath = Path.Combine(Path.GetTempPath(), "whatsinmyfridge", "photos");
                try
                {
                    Directory.CreateDirectory(_localPhotosPath);
                    _logger.LogInformation("Fallback local file storage initialized at: {LocalPath}", _localPhotosPath);
                }
                catch (Exception tempEx)
                {
                    _logger.LogError(tempEx, "Failed to create fallback storage directory. Photo uploads will fail.");
                    _localPhotosPath = string.Empty;
                }
            }
        }
        else
        {
            _localPhotosPath = string.Empty;
            _logger.LogInformation("BlobStorageService initialized using Azure Blob Storage");
        }
        
        _logger.LogInformation("BlobStorageService initialized. Using blob storage: {UseBlobStorage}", _useBlobStorage);
    }

    public async Task<string> UploadPhotoAsync(Stream photoStream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Uploading photo {FileName} with content type {ContentType}", fileName, contentType);
        
        if (_useBlobStorage && _blobServiceClient != null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            // Create container with private access (no public access)
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(fileName);
            await blobClient.UploadAsync(photoStream, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);

            _logger.LogInformation("Photo {FileName} uploaded to blob storage: {Uri}", fileName, blobClient.Uri);
            return blobClient.Uri.ToString();
        }
        else
        {
            // Fallback to local file storage
            var filePath = Path.Combine(_localPhotosPath, fileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await photoStream.CopyToAsync(fileStream, cancellationToken);
            }
            _logger.LogInformation("Photo {FileName} saved to local storage: {Path}", fileName, filePath);
            return $"/api/photos/{fileName}";
        }
    }
    public async Task DeletePhotoAsync(string photoId, CancellationToken cancellationToken)
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
                var deleted = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
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

    public async Task<(byte[] fileBytes, string contentType)?> GetPhotoAsync(string fileName, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving photo {FileName}", fileName);
        
        if (_useBlobStorage && _blobServiceClient != null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Photo {FileName} not found in blob storage", fileName);
                return null;
            }

            var downloadResult = await blobClient.DownloadAsync(cancellationToken);
            using var memoryStream = new MemoryStream();
            await downloadResult.Value.Content.CopyToAsync(memoryStream, cancellationToken);
            
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

            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            _logger.LogDebug("Photo {FileName} retrieved from local storage", fileName);
            return (fileBytes, contentType);
        }
    }

    public bool IsUsingBlobStorage => _useBlobStorage;
}
