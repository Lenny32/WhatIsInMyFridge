using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class AuthenticationService
{
    private readonly UserStore _userStore;
    private readonly HouseholdStore _householdStore;
    private readonly PasswordHasher _passwordHasher;

    public AuthenticationService(UserStore userStore, HouseholdStore householdStore, PasswordHasher passwordHasher)
    {
        _userStore = userStore;
        _householdStore = householdStore;
        _passwordHasher = passwordHasher;
    }

    public async Task<User?> AuthenticateAsync(string email, string password)
    {
        var user = await _userStore.GetByEmailAsync(email);
        if (user == null)
        {
            return null;
        }

        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            return null;
        }

        return user;
    }

    public async Task<(User user, Household household)?> RegisterAsync(string email, string password, string name, string householdName)
    {
        var existing = await _userStore.GetByEmailAsync(email);
        if (existing != null)
        {
            return null;
        }

        var household = new Household
        {
            Name = householdName,
        };

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(password),
            Name = name,
            CurrentHouseholdId = household.Id
        };

        household.OwnerId = user.Id;
        household.MemberIds.Add(user.Id);
        user.HouseholdIds.Add(household.Id);

        await _householdStore.CreateAsync(household);
        await _userStore.CreateAsync(user);

        return (user, household);
    }

    public async Task<ApplicationContext?> BuildContextAsync(string userId, string? householdId = null)
    {
        var user = await _userStore.GetByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var targetHouseholdId = householdId ?? user.CurrentHouseholdId;
        if (string.IsNullOrEmpty(targetHouseholdId))
        {
            return null;
        }

        var household = await _householdStore.GetByIdAsync(targetHouseholdId);
        if (household == null || !household.MemberIds.Contains(userId))
        {
            return null;
        }

        return new ApplicationContext
        {
            User = user,
            Household = household
        };
    }
}
