using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WhatIsInMyFridge.Api.HealthChecks;
using WhatIsInMyFridge.Api.Services;

namespace WhatIsInMyFridge.Api.Configuration;

internal static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddDataInfrastructure(this WebApplicationBuilder builder)
    {
        var cosmosSettings = DataConfiguration.GetCosmosSettings(builder.Configuration);
        builder.Configuration[$"ConnectionStrings:{DataConfiguration.CosmosConnectionName}"] = cosmosSettings.ConnectionString;

        builder.AddCosmosDbContext<AppDbContext>(
            DataConfiguration.CosmosConnectionName,
            cosmosSettings.DatabaseName);

        builder.Services.AddSingleton(cosmosSettings);
        builder.Services.AddSingleton(new CosmosClient(
            cosmosSettings.ConnectionString,
            new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway
            }));

        var blobSettings = DataConfiguration.GetBlobStorageSettings(builder.Configuration);
        builder.Configuration[$"ConnectionStrings:{DataConfiguration.BlobConnectionName}"] = blobSettings.ConnectionString;

        builder.Services.AddSingleton(new BlobServiceClient(blobSettings.ConnectionString));

        builder.Services.AddHealthChecks()
            .AddCheck<CosmosDbHealthCheck>("cosmosdb", tags: ["ready"])
            .AddCheck<BlobStorageHealthCheck>("blob-storage", tags: ["ready"]);

        return builder;
    }
}
