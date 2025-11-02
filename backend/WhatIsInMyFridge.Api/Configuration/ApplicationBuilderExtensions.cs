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
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
