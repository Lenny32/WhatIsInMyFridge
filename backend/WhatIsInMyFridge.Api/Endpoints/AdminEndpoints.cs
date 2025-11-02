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

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin");
        group.RequireAuthorization();

        group.MapGet("/users", async Task<IResult> (HttpContext httpContext, UserStore userStore) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var currentUser = await userStore.GetByIdAsync(userId);
            if (currentUser == null || !currentUser.IsAdmin)
            {
                return Results.Forbid();
            }

            var users = await userStore.GetAllUsersAsync();

            var userList = users.Select(u => new
            {
                u.Id,
                u.Email,
                u.Name,
                u.IsAdmin,
                u.CreatedAt,
                u.UpdatedAt
            });

            return Results.Ok(userList);
        });

        group.MapPost("/users/{id}/reset-password", async Task<IResult> (string id, ResetPasswordRequest request, HttpContext httpContext, UserStore userStore, PasswordHasher passwordHasher) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var currentUser = await userStore.GetByIdAsync(userId);
            if (currentUser == null || !currentUser.IsAdmin)
            {
                return Results.Forbid();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                return Results.ValidationProblem(errors);
            }

            var targetUser = await userStore.GetByIdAsync(id);
            if (targetUser == null)
            {
                return Results.NotFound();
            }

            targetUser.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
            await userStore.UpdateAsync(targetUser);

            return Results.Ok(new { message = "Password reset successfully" });
        });

        return endpoints;
    }
}
