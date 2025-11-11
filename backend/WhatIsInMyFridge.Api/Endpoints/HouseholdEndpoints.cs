using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Dtos;
using WhatIsInMyFridge.Api.Infrastructure;
using WhatIsInMyFridge.Api.Models;
using WhatIsInMyFridge.Api.Services;

namespace WhatIsInMyFridge.Api.Endpoints;

internal static class HouseholdEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/households");
        group.RequireAuthorization();

        group.MapGet(string.Empty, async Task<IResult> (HttpContext httpContext, HouseholdStore householdStore, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var userIdString = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Getting households for user {UserId}", userIdString);
            
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                logger.LogWarning("Unauthorized access to households endpoint");
                return Results.Unauthorized();
            }

            var households = await householdStore.GetByUserIdAsync(userId, cancellationToken);
            logger.LogInformation("Retrieved {HouseholdCount} households for user {UserId}", households.Count, userId);
            return Results.Ok(households);
        });

        group.MapPost(string.Empty, async Task<IResult> (CreateHouseholdRequest request, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var userIdString = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Creating household '{HouseholdName}' for user {UserId}", request.Name, userIdString);
            
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                logger.LogWarning("Unauthorized attempt to create household");
                return Results.Unauthorized();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                logger.LogWarning("Invalid household creation request for user {UserId}", userId);
                return Results.ValidationProblem(errors);
            }

            var user = await userStore.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                logger.LogWarning("User {UserId} not found during household creation", userId);
                return Results.Unauthorized();
            }

            var household = new Household
            {
                Name = request.Name,
                OwnerId = userId,
                MemberIds = new List<Guid> { userId }
            };

            await householdStore.CreateAsync(household, cancellationToken);

            user.HouseholdIds.Add(household.Id);
            await userStore.UpdateAsync(user, cancellationToken);
            
            logger.LogInformation("Created household {HouseholdId} for user {UserId}", household.Id, userId);

            return Results.Created($"/api/households/{household.Id}", household);
        });

        group.MapPost("/{householdId}/switch", async Task<IResult> (Guid householdId, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore, JwtTokenService jwtTokenService, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var userIdString = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("User {UserId} switching to household {HouseholdId}", userIdString, householdId);
            
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                logger.LogWarning("Unauthorized attempt to switch household");
                return Results.Unauthorized();
            }

            var household = await householdStore.GetByIdAsync(householdId, cancellationToken);
            if (household == null || !household.MemberIds.Contains(userId))
            {
                logger.LogWarning("User {UserId} attempted to switch to invalid or unauthorized household {HouseholdId}", userId, householdId);
                return Results.NotFound();
            }

            var user = await userStore.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                logger.LogWarning("User {UserId} not found during household switch", userId);
                return Results.Unauthorized();
            }

            user.CurrentHouseholdId = householdId;
            await userStore.UpdateAsync(user, cancellationToken);

            var token = jwtTokenService.GenerateToken(userId, householdId, user.IsAdmin);
            
            logger.LogInformation("User {UserId} successfully switched to household {HouseholdId}", userId, householdId);

            return Results.Ok(new { token, household });
        });

        group.MapPost("/{householdId}/members", async Task<IResult> (Guid householdId, AddHouseholdMemberRequest request, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var userIdString = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Adding member {Email} to household {HouseholdId} by user {UserId}", request.Email, householdId, userIdString);
            
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                logger.LogWarning("Unauthorized attempt to add household member");
                return Results.Unauthorized();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                logger.LogWarning("Invalid add household member request for household {HouseholdId}", householdId);
                return Results.ValidationProblem(errors);
            }

            var household = await householdStore.GetByIdAsync(householdId, cancellationToken);
            if (household == null)
            {
                logger.LogWarning("Household {HouseholdId} not found", householdId);
                return Results.NotFound();
            }

            if (household.OwnerId != userId)
            {
                logger.LogWarning("Non-owner user {UserId} attempted to add member to household {HouseholdId}", userId, householdId);
                return Results.Forbid();
            }

            var newMember = await userStore.GetByEmailAsync(request.Email, cancellationToken);
            if (newMember == null)
            {
                logger.LogWarning("User with email {Email} does not exist", request.Email);
                return Results.BadRequest(new { error = "User with this email does not exist" });
            }

            if (household.MemberIds.Contains(newMember.Id))
            {
                logger.LogWarning("User {MemberId} is already a member of household {HouseholdId}", newMember.Id, householdId);
                return Results.BadRequest(new { error = "User is already a member of this household" });
            }

            household.MemberIds.Add(newMember.Id);
            await householdStore.UpdateAsync(household, cancellationToken);

            newMember.HouseholdIds.Add(household.Id);
            await userStore.UpdateAsync(newMember, cancellationToken);
            
            logger.LogInformation("Successfully added member {MemberId} to household {HouseholdId}", newMember.Id, householdId);

            return Results.Ok(household);
        });

        group.MapDelete("/{householdId}/members/{memberId}", async Task<IResult> (Guid householdId, Guid memberId, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var userIdString = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Removing member {MemberId} from household {HouseholdId} by user {UserId}", memberId, householdId, userIdString);
            
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                logger.LogWarning("Unauthorized attempt to remove household member");
                return Results.Unauthorized();
            }

            var household = await householdStore.GetByIdAsync(householdId, cancellationToken);
            if (household == null)
            {
                logger.LogWarning("Household {HouseholdId} not found", householdId);
                return Results.NotFound();
            }

            if (household.OwnerId != userId && userId != memberId)
            {
                logger.LogWarning("User {UserId} not authorized to remove member {MemberId} from household {HouseholdId}", userId, memberId, householdId);
                return Results.Forbid();
            }

            if (!household.MemberIds.Contains(memberId))
            {
                logger.LogWarning("Member {MemberId} not found in household {HouseholdId}", memberId, householdId);
                return Results.NotFound();
            }

            if (household.OwnerId == memberId)
            {
                logger.LogWarning("Attempt to remove owner {MemberId} from household {HouseholdId}", memberId, householdId);
                return Results.BadRequest(new { error = "Cannot remove the owner from the household" });
            }

            household.MemberIds.Remove(memberId);
            await householdStore.UpdateAsync(household, cancellationToken);

            var member = await userStore.GetByIdAsync(memberId, cancellationToken);
            if (member != null)
            {
                member.HouseholdIds.Remove(household.Id);
                if (member.CurrentHouseholdId == householdId)
                {
                    member.CurrentHouseholdId = member.HouseholdIds.Count > 0 ? member.HouseholdIds.FirstOrDefault() : null;
                }

                await userStore.UpdateAsync(member, cancellationToken);
            }
            
            logger.LogInformation("Successfully removed member {MemberId} from household {HouseholdId}", memberId, householdId);

            return Results.NoContent();
        });

        return endpoints;
    }
}
