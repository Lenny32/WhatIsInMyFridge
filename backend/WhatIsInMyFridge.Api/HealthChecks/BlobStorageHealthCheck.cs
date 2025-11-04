using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WhatIsInMyFridge.Api.HealthChecks;

internal sealed class BlobStorageHealthCheck : IHealthCheck
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageHealthCheck(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _blobServiceClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("Blob Storage is reachable.");
        }
        catch (RequestFailedException ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Blob Storage request failed with status code {ex.Status}.",
                ex);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Blob Storage health check failed.",
                ex);
        }
    }
}
