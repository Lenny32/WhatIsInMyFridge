using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WhatIsInMyFridge.Api;
using WhatIsInMyFridge.Api.Services;

namespace WhatIsInMyFridge.Api.Configuration;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<UserStore>();
        services.AddScoped<HouseholdStore>();
        services.AddScoped<FoodInventoryStore>();
        services.AddScoped<RecipeStore>();
        services.AddScoped<GroceryListStore>();
        services.AddScoped<MealPlanStore>();
        services.AddSingleton<PasswordHasher>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<ApplicationContext>();
        services.AddScoped<JwtTokenService>();
        services.AddSingleton<BlobStorageService>();
        services.AddScoped<CosmosDbInitializer>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Handle both null and empty strings for JWT key
        var configKey = configuration["Jwt:Key"];
        var envKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        
        var jwtKey = !string.IsNullOrWhiteSpace(configKey) ? configKey
            : !string.IsNullOrWhiteSpace(envKey) ? envKey
            : throw new InvalidOperationException("JWT secret key is required. Provide 'Jwt:Key' in configuration or 'JWT_SECRET_KEY' environment variable.");
            
        var signingKeyBytes = JwtKeyUtility.GetSigningKeyBytes(jwtKey);
        var signingKey = new SymmetricSecurityKey(signingKeyBytes);
        var jwtIssuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer configuration is required.");
        var jwtAudience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience configuration is required.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = signingKey
            };
        });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddConfiguredCors(this IServiceCollection services)
    {
        var allowedOrigins = new List<string>();
        var envOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
        if (!string.IsNullOrWhiteSpace(envOrigins))
        {
            allowedOrigins.AddRange(envOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins.Count > 0)
                {
                    policy.WithOrigins(allowedOrigins.ToArray())
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                }
                else
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddJsonOptions(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        return services;
    }

    public static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services, IWebHostEnvironment environment)
    {
        if (environment.IsProduction())
        {
            return services;
        }

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SchemaFilter<StringEnumSchemaFilter>();
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter 'Bearer' followed by a space and your JWT token"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
