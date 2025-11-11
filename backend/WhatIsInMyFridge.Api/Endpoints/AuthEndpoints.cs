using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Infrastructure;
using WhatIsInMyFridge.Api.Services;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/register", async Task<IResult> (RegisterRequest request, AuthenticationService authService, JwtTokenService jwtTokenService, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            logger.LogInformation("User registration attempt for email: {Email}", request.Email);
            
            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                logger.LogWarning("Invalid registration request for email: {Email}", request.Email);
                return Results.ValidationProblem(errors);
            }

            var result = await authService.RegisterAsync(request.Email, request.Password, request.Name, request.HouseholdName, cancellationToken);
            if (result == null)
            {
                logger.LogWarning("Registration failed - User with email {Email} already exists", request.Email);
                return Results.BadRequest(new { error = "User with this email already exists" });
            }

            var token = jwtTokenService.GenerateToken(result.Value.user.Id, result.Value.household.Id, result.Value.user.IsAdmin);
            
            logger.LogInformation("User {UserId} successfully registered with household {HouseholdId}", result.Value.user.Id, result.Value.household.Id);

            return Results.Ok(new
            {
                token,
                user = new
                {
                    result.Value.user.Id,
                    result.Value.user.Email,
                    result.Value.user.Name,
                    result.Value.user.HouseholdIds,
                    result.Value.user.CurrentHouseholdId
                },
                household = result.Value.household
            });
        });

        group.MapPost("/login", async Task<IResult> (LoginRequest request, AuthenticationService authService, JwtTokenService jwtTokenService, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Login attempt for email: {Email}", request.Email);
            
            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                logger.LogWarning("Invalid login request for email: {Email}", request.Email);
                return Results.ValidationProblem(errors);
            }

            var user = await authService.AuthenticateAsync(request.Email, request.Password, cancellationToken);
            if (user == null)
            {
                logger.LogWarning("Failed login attempt for email: {Email}", request.Email);
                return Results.Unauthorized();
            }

            var householdId = user.CurrentHouseholdId ?? (user.HouseholdIds.Count > 0 ? user.HouseholdIds.FirstOrDefault() : (Guid?)null);
            if (householdId == null)
            {
                logger.LogWarning("User {UserId} has no household", user.Id);
                return Results.BadRequest(new { error = "User is not part of any household" });
            }

            var token = jwtTokenService.GenerateToken(user.Id, householdId.Value, user.IsAdmin);
            
            logger.LogInformation("User {UserId} successfully logged in to household {HouseholdId}", user.Id, householdId);

            return Results.Ok(new
            {
                token,
                user = new
                {
                    user.Id,
                    user.Email,
                    user.Name,
                    user.HouseholdIds,
                    user.CurrentHouseholdId
                }
            });
        });

        group.MapPost("/logout", (ILogger<Program> logger, HttpContext httpContext) => 
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("User {UserId} logged out", userId);
            return Results.NoContent();
        });

        group.MapGet("/me", async Task<IResult> (HttpContext httpContext, UserStore userStore, HouseholdStore householdStore, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Getting current user info for user: {UserId}", userId);
            
            if (string.IsNullOrEmpty(userId))
            {
                logger.LogWarning("Unauthorized access to /me endpoint");
                return Results.Unauthorized();
            }

            // Parse userId as Guid
            if (!Guid.TryParse(userId, out var userGuid))
            {
                logger.LogWarning("Invalid user ID format: {UserId}", userId);
                return Results.BadRequest("Invalid user ID format");
            }

            var user = await userStore.GetByIdAsync(userGuid, cancellationToken);
            if (user == null)
            {
                logger.LogWarning("User {UserId} not found in database", userId);
                return Results.Unauthorized();
            }

            var householdIdClaim = httpContext.User.FindFirst("householdId")?.Value;
            Household? household = null;
            
            if (!string.IsNullOrEmpty(householdIdClaim) && Guid.TryParse(householdIdClaim, out var householdGuid))
            {
                household = await householdStore.GetByIdAsync(householdGuid, cancellationToken);
            }

            return Results.Ok(new
            {
                user = new
                {
                    user.Id,
                    user.Email,
                    user.Name,
                    user.HouseholdIds,
                    user.CurrentHouseholdId
                },
                household
            });
        }).RequireAuthorization();

        return endpoints;
    }
}
