using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WhatIsInMyFridge.Api.Configuration;

internal static class DataConfiguration
{
    public const string CosmosConnectionName = "CosmosDb";
    public const string BlobConnectionName = "BlobStorage";

    public static CosmosDbSettings GetCosmosSettings(IConfiguration configuration)
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("DataConfiguration");
        
        var connectionString = RequireConnectionString(
            configuration,
            CosmosConnectionName,
            fallbackConfigurationKeys: new[]
            {
                "CosmosDb:ConnectionString",
                "CosmosDb__ConnectionString"
            },
            fallbackEnvironmentVariables: new[]
            {
                "COSMOSDB_CONNECTION_STRING",
                "COSMOS_CONNECTION_STRING"
            });

        var databaseName = RequireSetting(
            configuration,
            keys: new[]
            {
                "CosmosDb:DatabaseName",
                "CosmosDb__DatabaseName"
            },
            fallbackEnvironmentVariables: new[]
            {
                "COSMOSDB_DATABASE_NAME",
                "COSMOS_DATABASE_NAME"
            },
            description: "Cosmos DB database name");

        // Log the configuration for debugging
        logger.LogInformation("Cosmos DB Configuration - Database: {DatabaseName}, Connection String Length: {ConnectionStringLength}", 
            databaseName, connectionString?.Length ?? 0);

        return new CosmosDbSettings(connectionString, databaseName);
    }

    public static BlobStorageSettings GetBlobStorageSettings(IConfiguration configuration) =>
        new BlobStorageSettings(
            RequireConnectionString(
                configuration,
                BlobConnectionName,
                fallbackConfigurationKeys: new[]
                {
                    "BlobStorage:ConnectionString",
                    "BlobStorage__ConnectionString"
                },
                fallbackEnvironmentVariables: new[]
                {
                    "BLOB_STORAGE_CONNECTION_STRING",
                    "AZURE_STORAGE_CONNECTION_STRING",
                    "STORAGE_CONNECTION_STRING"
                }));

    private static string RequireConnectionString(
        IConfiguration configuration,
        string name,
        IEnumerable<string> fallbackConfigurationKeys,
        IEnumerable<string> fallbackEnvironmentVariables)
    {
        var candidates = new List<string?>
        {
            configuration.GetConnectionString(name),
            configuration[$"ConnectionStrings:{name}"],
            configuration[$"ConnectionStrings__{name}"]
        };

        candidates.AddRange(fallbackConfigurationKeys.Select(key => configuration[key]));
        candidates.AddRange(fallbackEnvironmentVariables.Select(Environment.GetEnvironmentVariable));

        var value = candidates.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value!;
        }

        // Special case for development: use local emulator if no connection string is found
        if (IsRunningInDevelopment(configuration))
        {
            var localEmulatorConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            return localEmulatorConnectionString;
        }

        var configTargets = new[] { $"ConnectionStrings:{name}" }
            .Concat(fallbackConfigurationKeys)
            .Distinct()
            .ToArray();

        throw new InvalidOperationException(
            $"Connection string '{name}' is required. Provide {string.Join(" or ", configTargets.Select(t => $"'{t}'"))}{FormatEnvHint(fallbackEnvironmentVariables)}.");
    }

    private static string RequireSetting(
        IConfiguration configuration,
        IEnumerable<string> keys,
        IEnumerable<string> fallbackEnvironmentVariables,
        string description)
    {
        var value = keys
            .Select(key => configuration[key])
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value!;
        }

        var envValue = fallbackEnvironmentVariables
            .Select(Environment.GetEnvironmentVariable)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue!;
        }

        // For database name, provide a default in development
        if (description.Contains("database name", StringComparison.OrdinalIgnoreCase) && IsRunningInDevelopment(configuration))
        {
            return "WhatIsInMyFridge";
        }

        throw new InvalidOperationException(
            $"{description} is required. Provide {string.Join(" or ", keys.Select(k => $"'{k}'"))}{FormatEnvHint(fallbackEnvironmentVariables)}.");
    }

    private static bool IsRunningInDevelopment(IConfiguration configuration)
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatEnvHint(IEnumerable<string> environmentVariables)
    {
        var candidates = environmentVariables?.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray() ?? [];
        return candidates.Length == 0
            ? string.Empty
            : $" or environment variable {string.Join(" / ", candidates.Select(v => $"'{v}'"))}";
    }
}

public sealed record CosmosDbSettings(string ConnectionString, string DatabaseName);

public sealed record BlobStorageSettings(string ConnectionString);

