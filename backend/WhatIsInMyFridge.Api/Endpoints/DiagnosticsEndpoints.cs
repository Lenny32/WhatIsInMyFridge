using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        endpoints.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        if (environment.IsDevelopment())
        {
            endpoints.MapGet("/api/debug/throw-exception", () =>
            {
                throw new InvalidOperationException("This is a test exception to verify debug mode exception serialization");
            });
        }

        return endpoints;
    }
}
