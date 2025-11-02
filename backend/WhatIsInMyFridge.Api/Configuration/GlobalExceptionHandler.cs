using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Dtos;

namespace WhatIsInMyFridge.Api.Configuration;

internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _logger = logger;
        _environment = environment;
        _configuration = configuration;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var includeDetailedErrors = ShouldIncludeDetailedErrors();

        var errorResponse = includeDetailedErrors
            ? CreateDetailedErrorResponse(exception)
            : new ErrorResponse
            {
                Type = "InternalServerError",
                Message = "An unexpected error occurred. Please try again later."
            };

        errorResponse.TraceId = httpContext.TraceIdentifier;

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = includeDetailedErrors
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(errorResponse, serializerOptions);
        await httpContext.Response.WriteAsync(payload, cancellationToken);

        return true;
    }

    private bool ShouldIncludeDetailedErrors()
    {
        var include =
            _environment.IsDevelopment()
            || _configuration.GetValue<bool>("Diagnostics:IncludeExceptionDetails")
            || string.Equals(_configuration["ASPNETCORE_DETAILEDERRORS"], "true", StringComparison.OrdinalIgnoreCase);

#if DEBUG
        include = true;
#endif

        return include;
    }

    private static ErrorResponse CreateDetailedErrorResponse(Exception exception)
    {
        return new ErrorResponse
        {
            Type = exception.GetType().FullName ?? "UnknownException",
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            Source = exception.Source,
            InnerException = exception.InnerException != null
                ? CreateDetailedErrorResponse(exception.InnerException)
                : null
        };
    }
}
