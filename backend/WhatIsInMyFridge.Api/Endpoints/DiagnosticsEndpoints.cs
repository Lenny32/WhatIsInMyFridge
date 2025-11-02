using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        endpoints.MapGet(
            "/health",
            async (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
            {
                var report = await healthChecks.CheckHealthAsync(
                    registration => registration.Tags.Contains("ready"),
                    cancellationToken);

                var response = new
                {
                    status = report.Status.ToString(),
                    durationMs = report.TotalDuration.TotalMilliseconds,
                    checks = report.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        durationMs = entry.Value.Duration.TotalMilliseconds,
                        description = entry.Value.Description
                    })
                };

                var statusCode = report.Status == HealthStatus.Healthy
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status503ServiceUnavailable;

                return Results.Json(response, statusCode: statusCode);
            });

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
