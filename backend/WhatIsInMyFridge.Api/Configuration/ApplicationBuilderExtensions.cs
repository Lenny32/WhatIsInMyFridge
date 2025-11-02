using System;
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
        app.UseExceptionHandler(_ => { }); // Use registered IExceptionHandler implementations
        return app;
    }

    public static async Task EnsureCosmosDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        if (!ShouldInitializeCosmosDatabase(app, scope.ServiceProvider))
        {
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    private static bool ShouldInitializeCosmosDatabase(WebApplication app, IServiceProvider services)
    {
        if (!app.Environment.IsDevelopment())
        {
            return false;
        }

        var settings = services.GetRequiredService<CosmosDbSettings>();
        return IsLocalCosmosEndpoint(settings.ConnectionString);
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
