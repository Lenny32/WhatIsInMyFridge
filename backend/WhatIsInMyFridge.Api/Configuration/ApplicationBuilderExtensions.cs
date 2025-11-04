using System;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WhatIsInMyFridge.Api.Services;

namespace WhatIsInMyFridge.Api.Configuration;

internal static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseDevelopmentExceptionSerialization(this IApplicationBuilder app)
    {
    var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
    var logger = loggerFactory?.CreateLogger("ApplicationBuilderExtensions");
    logger?.LogDebug("Registering exception handler middleware (development serialization)");

        app.UseExceptionHandler(_ => { }); // Use registered IExceptionHandler implementations

        logger?.LogDebug("Exception handler middleware registered");
        return app;
    }

    public static async Task EnsureCosmosDatabaseAsync(this WebApplication app)
    {
    using var scope = app.Services.CreateScope();
    var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
    var logger = loggerFactory?.CreateLogger("ApplicationBuilderExtensions");

        try
        {
            logger?.LogInformation("Checking whether to initialize Cosmos DB database");

            if (!ShouldInitializeCosmosDatabase(app, scope.ServiceProvider))
            {
                logger?.LogDebug("Skipping Cosmos DB initialization (not development or not local endpoint)");
                return;
            }

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            logger?.LogInformation("Ensuring Cosmos DB database is created (EnsureCreatedAsync)");
            await dbContext.Database.EnsureCreatedAsync();
            logger?.LogInformation("Cosmos DB database ensured/created successfully");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to ensure Cosmos DB database");
            // Don't rethrow - initialization should not stop the application from starting
        }
    }

    private static bool ShouldInitializeCosmosDatabase(WebApplication app, IServiceProvider services)
    {
    var loggerFactory = services.GetService<ILoggerFactory>();
    var logger = loggerFactory?.CreateLogger("ApplicationBuilderExtensions");

        if (!app.Environment.IsDevelopment())
        {
            logger?.LogDebug("Not a development environment; skipping Cosmos DB initialization");
            return false;
        }

        CosmosDbSettings settings;
        try
        {
            settings = services.GetRequiredService<CosmosDbSettings>();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "CosmosDbSettings not available from service provider; skipping initialization");
            return false;
        }

        var isLocal = IsLocalCosmosEndpoint(settings.ConnectionString);
        logger?.LogDebug("Detected local Cosmos endpoint: {IsLocal}", isLocal);
        return isLocal;
    }

    private static bool IsLocalCosmosEndpoint(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        return connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase)
               || connectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || connectionString.Contains("AccountEndpoint=https://host.docker.internal", StringComparison.OrdinalIgnoreCase);
    }
}
