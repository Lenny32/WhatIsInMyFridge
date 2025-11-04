using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WhatIsInMyFridge.Api.Configuration;

namespace WhatIsInMyFridge.Api.HealthChecks;

internal sealed class CosmosDbHealthCheck : IHealthCheck
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbSettings _settings;

    public CosmosDbHealthCheck(CosmosClient cosmosClient, CosmosDbSettings settings)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _cosmosClient
                .GetDatabase(_settings.DatabaseName)
                .ReadAsync(cancellationToken: cancellationToken);

            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent or HttpStatusCode.NotModified)
            {
                return HealthCheckResult.Healthy("Cosmos DB is reachable.");
            }

            return HealthCheckResult.Unhealthy(
                $"Cosmos DB responded with status code {(int)response.StatusCode} ({response.StatusCode}).");
        }
        catch (CosmosException ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Cosmos DB request failed with status code {(int)ex.StatusCode} ({ex.StatusCode}).",
                ex);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Cosmos DB health check failed.",
                ex);
        }
    }
}
