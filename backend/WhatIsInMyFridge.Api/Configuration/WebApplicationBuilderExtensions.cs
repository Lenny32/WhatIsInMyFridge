using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.HealthChecks;
using WhatIsInMyFridge.Api.Services;

namespace WhatIsInMyFridge.Api.Configuration;

internal static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddDataInfrastructure(this WebApplicationBuilder builder)
    {
        var logger = builder.Services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("DataInfrastructure");
        
        try
        {
            logger?.LogInformation("Configuring Cosmos DB settings...");
            var cosmosSettings = DataConfiguration.GetCosmosSettings(builder.Configuration);
            logger?.LogInformation("Cosmos DB settings: ConnectionString={ConnectionString}, DatabaseName={DatabaseName}", 
                MaskConnectionString(cosmosSettings.ConnectionString), cosmosSettings.DatabaseName);
            
            builder.Configuration[$"ConnectionStrings:{DataConfiguration.CosmosConnectionName}"] = cosmosSettings.ConnectionString;

            builder.AddCosmosDbContext<AppDbContext>(
                DataConfiguration.CosmosConnectionName,
                cosmosSettings.DatabaseName,
                configureDbContextOptions: options =>
                {
                    // Suppress Azure Cosmos DB synchronous I/O warning
                    // Azure Cosmos DB does not support synchronous operations
                    // All code should use async methods (e.g., SaveChangesAsync, ToListAsync, etc.)
                    options.ConfigureWarnings(warnings =>
                    {
                        warnings.Ignore(CosmosEventId.SyncNotSupported);
                    });
                });

            builder.Services.AddSingleton(cosmosSettings);
            builder.Services.AddSingleton(serviceProvider =>
            {
                logger?.LogInformation("Creating CosmosClient...");
                return new CosmosClient(
                    cosmosSettings.ConnectionString,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        SerializerOptions = new CosmosSerializationOptions
                        {
                            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                        }
                    });
            });

            logger?.LogInformation("Configuring Blob Storage settings...");
            var blobSettings = DataConfiguration.GetBlobStorageSettings(builder.Configuration);
            builder.Configuration[$"ConnectionStrings:{DataConfiguration.BlobConnectionName}"] = blobSettings.ConnectionString;

            builder.Services.AddSingleton(new BlobServiceClient(blobSettings.ConnectionString));

            builder.Services.AddHealthChecks()
                .AddCheck<CosmosDbHealthCheck>("cosmosdb", tags: ["ready"])
                .AddCheck<BlobStorageHealthCheck>("blob-storage", tags: ["ready"]);

            logger?.LogInformation("Data infrastructure configured successfully");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to configure data infrastructure");
            throw;
        }

        return builder;
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "[empty]";
        
        // Mask the account key for security
        if (connectionString.Contains("AccountKey="))
        {
            var parts = connectionString.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("AccountKey="))
                {
                    parts[i] = "AccountKey=***MASKED***";
                }
            }
            return string.Join(';', parts);
        }
        
        return connectionString.Length > 50 ? connectionString.Substring(0, 47) + "..." : connectionString;
    }
}
