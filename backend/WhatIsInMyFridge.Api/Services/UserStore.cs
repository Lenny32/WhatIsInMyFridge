using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class UserStore
{
    private readonly AppDbContext _context;
    private readonly ILogger<UserStore> _logger;

    public UserStore(AppDbContext context, ILogger<UserStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<User> CreateAsync(User user)
    {
        _logger.LogInformation("Creating user {UserId} with email {Email}", user.Id, user.Email);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _logger.LogInformation("User {UserId} created successfully", user.Id);
        return user;
    }

    public async Task<User?> UpdateAsync(User user)
    {
        _logger.LogInformation("Updating user {UserId}", user.Id);
        var existing = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        
        if (existing == null)
        {
            _logger.LogWarning("Update failed: User {UserId} not found", user.Id);
            return null;
        }

        // Update timestamps before saving
        user.UpdatedAt = DateTimeOffset.UtcNow;
        
        // Attach and mark as modified - this respects the partition key
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User {UserId} updated successfully", user.Id);
        return user;
    }
    
    public async Task<List<User>> GetAllUsersAsync()
    {
        _logger.LogDebug("Retrieving all users");
        var users = await _context.Users
            .OrderBy(u => u.Email)
            .ToListAsync();
        _logger.LogDebug("Retrieved {Count} users", users.Count);
        return users;
    }
}
