using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class AuthenticationService
{
    private readonly UserStore _userStore;
    private readonly HouseholdStore _householdStore;
    private readonly PasswordHasher _passwordHasher;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        UserStore userStore, 
        HouseholdStore householdStore, 
        PasswordHasher passwordHasher,
        ILogger<AuthenticationService> logger)
    {
        _userStore = userStore;
        _householdStore = householdStore;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<User?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting authentication for user {Email}", email);
        
        var user = await _userStore.GetByEmailAsync(email, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("Authentication failed: User not found for email {Email}", email);
            return null;
        }

        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            _logger.LogWarning("Authentication failed: Invalid password for user {UserId}", user.Id);
            return null;
        }

        _logger.LogInformation("User {UserId} authenticated successfully", user.Id);
        return user;
    }

    public async Task<(User user, Household household)?> RegisterAsync(string email, string password, string name, string householdName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting registration for user {Email} with household {HouseholdName}", email, householdName);
        
        var existing = await _userStore.GetByEmailAsync(email, cancellationToken);
        if (existing != null)
        {
            _logger.LogWarning("Registration failed: Email {Email} already exists", email);
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

        await _householdStore.CreateAsync(household, cancellationToken);
        await _userStore.CreateAsync(user, cancellationToken);

        _logger.LogInformation("User {UserId} registered successfully with household {HouseholdId}", user.Id, household.Id);
        return (user, household);
    }

    public async Task<ApplicationContext?> BuildContextAsync(Guid userId, Guid? householdId = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Building application context for user {UserId}, household {HouseholdId}", userId, householdId?.ToString() ?? "current");
        
        var user = await _userStore.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("Failed to build context: User {UserId} not found", userId);
            return null;
        }

        var targetHouseholdId = householdId ?? user.CurrentHouseholdId;
        if (targetHouseholdId == null)
        {
            _logger.LogWarning("Failed to build context: No household ID for user {UserId}", userId);
            return null;
        }

        var household = await _householdStore.GetByIdAsync(targetHouseholdId.Value, cancellationToken);
        if (household == null || !household.MemberIds.Contains(userId))
        {
            _logger.LogWarning("Failed to build context: User {UserId} not member of household {HouseholdId}", userId, targetHouseholdId);
            return null;
        }

        _logger.LogDebug("Successfully built context for user {UserId} in household {HouseholdId}", userId, household.Id);
        return new ApplicationContext
        {
            User = user,
            Household = household
        };
    }
}
