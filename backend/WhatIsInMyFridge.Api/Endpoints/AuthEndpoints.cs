using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Infrastructure;
using WhatIsInMyFridge.Api.Services;

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/register", async Task<IResult> (RegisterRequest request, AuthenticationService authService, JwtTokenService jwtTokenService) =>
        {
            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                return Results.ValidationProblem(errors);
            }

            var result = await authService.RegisterAsync(request.Email, request.Password, request.Name, request.HouseholdName);
            if (result == null)
            {
                return Results.BadRequest(new { error = "User with this email already exists" });
            }

            var token = jwtTokenService.GenerateToken(result.Value.user.Id, result.Value.household.Id);

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

        group.MapPost("/login", async Task<IResult> (LoginRequest request, AuthenticationService authService, JwtTokenService jwtTokenService) =>
        {
            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                return Results.ValidationProblem(errors);
            }

            var user = await authService.AuthenticateAsync(request.Email, request.Password);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            var householdId = user.CurrentHouseholdId ?? user.HouseholdIds.FirstOrDefault();
            if (string.IsNullOrEmpty(householdId))
            {
                return Results.BadRequest(new { error = "User is not part of any household" });
            }

            var token = jwtTokenService.GenerateToken(user.Id, householdId);

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

        group.MapPost("/logout", () => Results.NoContent());

        group.MapGet("/me", async Task<IResult> (HttpContext httpContext, UserStore userStore, HouseholdStore householdStore) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var user = await userStore.GetByIdAsync(userId);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            var householdId = httpContext.User.FindFirst("householdId")?.Value;
            var household = !string.IsNullOrEmpty(householdId)
                ? await householdStore.GetByIdAsync(householdId)
                : null;

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
