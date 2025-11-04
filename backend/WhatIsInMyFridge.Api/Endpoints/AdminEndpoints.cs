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

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin");
        group.RequireAuthorization();

        group.MapGet("/users", async Task<IResult> (HttpContext httpContext, UserStore userStore, ILogger<Program> logger) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Getting all users - Requested by user: {UserId}", userId);
            
            if (string.IsNullOrEmpty(userId))
            {
                logger.LogWarning("Unauthorized access attempt to get all users");
                return Results.Unauthorized();
            }

            var currentUser = await userStore.GetByIdAsync(userId);
            if (currentUser == null || !currentUser.IsAdmin)
            {
                logger.LogWarning("Non-admin user {UserId} attempted to access admin endpoint", userId);
                return Results.Forbid();
            }

            var users = await userStore.GetAllUsersAsync();
            logger.LogInformation("Retrieved {UserCount} users for admin {UserId}", users.Count(), userId);

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

        group.MapPost("/users/{id}/reset-password", async Task<IResult> (string id, ResetPasswordRequest request, HttpContext httpContext, UserStore userStore, PasswordHasher passwordHasher, ILogger<Program> logger) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Resetting password for user {TargetUserId} - Requested by {AdminUserId}", id, userId);
            
            if (string.IsNullOrEmpty(userId))
            {
                logger.LogWarning("Unauthorized password reset attempt for user {TargetUserId}", id);
                return Results.Unauthorized();
            }

            var currentUser = await userStore.GetByIdAsync(userId);
            if (currentUser == null || !currentUser.IsAdmin)
            {
                logger.LogWarning("Non-admin user {UserId} attempted to reset password for {TargetUserId}", userId, id);
                return Results.Forbid();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                logger.LogWarning("Invalid password reset request for user {TargetUserId}", id);
                return Results.ValidationProblem(errors);
            }

            var targetUser = await userStore.GetByIdAsync(id);
            if (targetUser == null)
            {
                logger.LogWarning("Password reset failed - User {TargetUserId} not found", id);
                return Results.NotFound();
            }

            targetUser.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
            await userStore.UpdateAsync(targetUser);
            
            logger.LogInformation("Password successfully reset for user {TargetUserId} by admin {AdminUserId}", id, userId);

            return Results.Ok(new { message = "Password reset successfully" });
        });

        return endpoints;
    }
}
