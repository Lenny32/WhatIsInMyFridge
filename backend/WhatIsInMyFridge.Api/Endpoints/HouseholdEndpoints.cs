using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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

        group.MapGet(string.Empty, async Task<IResult> (HttpContext httpContext, HouseholdStore householdStore) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var households = await householdStore.GetByUserIdAsync(userId);
            return Results.Ok(households);
        });

        group.MapPost(string.Empty, async Task<IResult> (CreateHouseholdRequest request, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                return Results.ValidationProblem(errors);
            }

            var user = await userStore.GetByIdAsync(userId);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            var household = new Household
            {
                Name = request.Name,
                OwnerId = userId,
                MemberIds = new List<string> { userId }
            };

            await householdStore.CreateAsync(household);

            user.HouseholdIds.Add(household.Id);
            await userStore.UpdateAsync(user);

            return Results.Created($"/api/households/{household.Id}", household);
        });

        group.MapPost("/{householdId}/switch", async Task<IResult> (string householdId, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore, JwtTokenService jwtTokenService) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var household = await householdStore.GetByIdAsync(householdId);
            if (household == null || !household.MemberIds.Contains(userId))
            {
                return Results.NotFound();
            }

            var user = await userStore.GetByIdAsync(userId);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            user.CurrentHouseholdId = householdId;
            await userStore.UpdateAsync(user);

            var token = jwtTokenService.GenerateToken(userId, householdId, user.IsAdmin);

            return Results.Ok(new { token, household });
        });

        group.MapPost("/{householdId}/members", async Task<IResult> (string householdId, AddHouseholdMemberRequest request, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            if (!ValidationHelper.TryValidate(request, out var errors))
            {
                return Results.ValidationProblem(errors);
            }

            var household = await householdStore.GetByIdAsync(householdId);
            if (household == null)
            {
                return Results.NotFound();
            }

            if (household.OwnerId != userId)
            {
                return Results.Forbid();
            }

            var newMember = await userStore.GetByEmailAsync(request.Email);
            if (newMember == null)
            {
                return Results.BadRequest(new { error = "User with this email does not exist" });
            }

            if (household.MemberIds.Contains(newMember.Id))
            {
                return Results.BadRequest(new { error = "User is already a member of this household" });
            }

            household.MemberIds.Add(newMember.Id);
            await householdStore.UpdateAsync(household);

            newMember.HouseholdIds.Add(household.Id);
            await userStore.UpdateAsync(newMember);

            return Results.Ok(household);
        });

        group.MapDelete("/{householdId}/members/{memberId}", async Task<IResult> (string householdId, string memberId, HttpContext httpContext, HouseholdStore householdStore, UserStore userStore) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var household = await householdStore.GetByIdAsync(householdId);
            if (household == null)
            {
                return Results.NotFound();
            }

            if (household.OwnerId != userId && userId != memberId)
            {
                return Results.Forbid();
            }

            if (!household.MemberIds.Contains(memberId))
            {
                return Results.NotFound();
            }

            if (household.OwnerId == memberId)
            {
                return Results.BadRequest(new { error = "Cannot remove the owner from the household" });
            }

            household.MemberIds.Remove(memberId);
            await householdStore.UpdateAsync(household);

            var member = await userStore.GetByIdAsync(memberId);
            if (member != null)
            {
                member.HouseholdIds.Remove(household.Id);
                if (member.CurrentHouseholdId == household.Id)
                {
                    member.CurrentHouseholdId = member.HouseholdIds.FirstOrDefault();
                }

                await userStore.UpdateAsync(member);
            }

            return Results.NoContent();
        });

        return endpoints;
    }
}
