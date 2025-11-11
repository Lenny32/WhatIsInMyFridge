using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Configuration;

namespace WhatIsInMyFridge.Api.Services;

public sealed class CosmosDbInitializer
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly ILogger<CosmosDbInitializer> _logger;

    public CosmosDbInitializer(CosmosClient cosmosClient, CosmosDbSettings settings, ILogger<CosmosDbInitializer> logger)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _databaseName = settings?.DatabaseName ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing Cosmos DB database and containers...");

            // Create database if it doesn't exist
            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                _databaseName, 
                cancellationToken: cancellationToken);
            
            _logger.LogInformation("Database '{DatabaseName}' {Status}", 
                _databaseName, 
                databaseResponse.StatusCode == System.Net.HttpStatusCode.Created ? "created" : "already exists");

            var database = databaseResponse.Database;

            // Define containers with their partition keys
            var containers = new[]
            {
                new { Name = "Users", PartitionKey = "/id" },
                new { Name = "Households", PartitionKey = "/id" },
                new { Name = "FoodItems", PartitionKey = "/id" },
                new { Name = "Recipes", PartitionKey = "/id" },
                new { Name = "GroceryItems", PartitionKey = "/id" }
            };

            // Create each container
            foreach (var container in containers)
            {
                var containerResponse = await database.CreateContainerIfNotExistsAsync(
                    container.Name,
                    container.PartitionKey,
                    throughput: 400, // Minimum throughput for serverless
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation("Container '{ContainerName}' with partition key '{PartitionKey}' {Status}",
                    container.Name,
                    container.PartitionKey,
                    containerResponse.StatusCode == System.Net.HttpStatusCode.Created ? "created" : "already exists");
            }

            _logger.LogInformation("Cosmos DB initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos DB");
            throw;
        }
    }
}