using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
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

        var blobSettings = DataConfiguration.GetBlobStorageSettings(builder.Configuration);
        builder.Configuration[$"ConnectionStrings:{DataConfiguration.BlobConnectionName}"] = blobSettings.ConnectionString;

        builder.Services.AddSingleton(new BlobServiceClient(blobSettings.ConnectionString));

        return builder;
    }
}
