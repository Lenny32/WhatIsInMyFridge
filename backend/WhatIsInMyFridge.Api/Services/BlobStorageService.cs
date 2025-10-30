using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class BlobStorageService
{
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly string _containerName;
    private readonly bool _useBlobStorage;
    private readonly string _localPhotosPath;

    public BlobStorageService(BlobServiceClient? blobServiceClient, IConfiguration configuration, IWebHostEnvironment environment)
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
    }

    public async Task<string> UploadPhotoAsync(Stream photoStream, string fileName, string contentType)
    {
        if (_useBlobStorage && _blobServiceClient != null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = containerClient.GetBlobClient(fileName);
            await blobClient.UploadAsync(photoStream, new BlobHttpHeaders { ContentType = contentType });

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
            return $"/api/photos/{fileName}";
        }
    }

    public async Task DeletePhotoAsync(string photoId)
    {
        if (_useBlobStorage && _blobServiceClient != null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            
            // Try common image extensions
            var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            foreach (var ext in extensions)
            {
                var fileName = $"{photoId}{ext}";
                var blobClient = containerClient.GetBlobClient(fileName);
                await blobClient.DeleteIfExistsAsync();
            }
        }
        else
        {
            // Fallback to local file storage - find and delete any file with this photoId
            var matchingFiles = Directory.GetFiles(_localPhotosPath, $"{photoId}.*");
            foreach (var file in matchingFiles)
            {
                File.Delete(file);
            }
        }
    }

    public async Task<(byte[] fileBytes, string contentType)?> GetPhotoAsync(string fileName)
    {
        if (_useBlobStorage && _blobServiceClient != null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync())
            {
                return null;
            }

            var downloadResult = await blobClient.DownloadAsync();
            using var memoryStream = new MemoryStream();
            await downloadResult.Value.Content.CopyToAsync(memoryStream);
            
            return (memoryStream.ToArray(), downloadResult.Value.ContentType);
        }
        else
        {
            // Fallback to local file storage
            var filePath = Path.Combine(_localPhotosPath, fileName);
            if (!File.Exists(filePath))
            {
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
            return (fileBytes, contentType);
        }
    }

    public bool IsUsingBlobStorage => _useBlobStorage;
}
