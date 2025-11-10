using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace WhatIsInMyFridge.Api.Services;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly string _jwtKey;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Handle both null and empty strings for JWT key
        var configKey = configuration["Jwt:Key"];
        var envKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        
        _jwtKey = !string.IsNullOrWhiteSpace(configKey) ? configKey
            : !string.IsNullOrWhiteSpace(envKey) ? envKey
            : throw new InvalidOperationException("JWT secret key is required. Provide 'Jwt:Key' in configuration or 'JWT_SECRET_KEY' environment variable.");
            
        _jwtIssuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer configuration is required.");
        _jwtAudience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience configuration is required.");

        _signingKey = new SymmetricSecurityKey(JwtKeyUtility.GetSigningKeyBytes(_jwtKey));
        
        _logger.LogInformation(" JwtTokenService initialized with issuer: {Issuer}, audience: {Audience}", _jwtIssuer, _jwtAudience);
    }

    public string GenerateToken(string userId, string householdId, bool isAdmin = false)
    {
        _logger.LogInformation("Generating JWT token for user {UserId}, household {HouseholdId}, isAdmin: {IsAdmin}", 
            userId, householdId, isAdmin);
        
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim("householdId", householdId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogDebug("JWT token generated successfully for user {UserId}", userId);
        return tokenString;
    }
}
