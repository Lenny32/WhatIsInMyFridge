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

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating user {UserId} with email {Email}", user.Id, user.Email);
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} created successfully", user.Id);
        return user;
    }

    public async Task<User?> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating user {UserId}", user.Id);
        var existing = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);
        
        if (existing == null)
        {
            _logger.LogWarning("Update failed: User {UserId} not found", user.Id);
            return null;
        }

        // Update timestamps before saving
        user.UpdatedAt = DateTimeOffset.UtcNow;
        
        // Attach and mark as modified - this respects the partition key
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("User {UserId} updated successfully", user.Id);
        return user;
    }
    
    public async Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all users");
        var users = await _context.Users
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);
        _logger.LogDebug("Retrieved {Count} users", users.Count);
        return users;
    }
}
