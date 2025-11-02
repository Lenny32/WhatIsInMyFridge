using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace WhatIsInMyFridge.Api.Configuration;

internal static class DataConfiguration
{
    public const string CosmosConnectionName = "CosmosDb";
    public const string BlobConnectionName = "BlobStorage";

    public static CosmosDbSettings GetCosmosSettings(IConfiguration configuration) =>
        new CosmosDbSettings(
            RequireConnectionString(
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
                }),
            RequireSetting(
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
                description: "Cosmos DB database name"));

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

        throw new InvalidOperationException(
            $"{description} is required. Provide {string.Join(" or ", keys.Select(k => $"'{k}'"))}{FormatEnvHint(fallbackEnvironmentVariables)}.");
    }

    private static string FormatEnvHint(IEnumerable<string> environmentVariables)
    {
        var candidates = environmentVariables?.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray() ?? [];
        return candidates.Length == 0
            ? string.Empty
            : $" or environment variable {string.Join(" / ", candidates.Select(v => $"'{v}'"))}";
    }
}

internal sealed record CosmosDbSettings(string ConnectionString, string DatabaseName);

internal sealed record BlobStorageSettings(string ConnectionString);

