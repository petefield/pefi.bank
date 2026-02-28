using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Pefi.Bank.Infrastructure;

namespace Pefi.Bank.Auth;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPefiAuthentication(
        this IServiceCollection services,
        JwtSettings jwtSettings,
        string cosmosConnectionString,
        string databaseName)
    {
        services.AddSingleton(jwtSettings);
        services.AddSingleton<JwtTokenService>();

        // Register the user store with its own Cosmos container
        services.AddSingleton<IUserStore<ApplicationUser>>(sp =>
        {
            var client = sp.GetRequiredService<CosmosClientHolder>().Client;
            var container = client.GetContainer(databaseName, "users");
            return new CosmosUserStore(container);
        });

        // Configure Identity (no EF, custom store)
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddDefaultTokenProviders();

        // Configure JWT authentication
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
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
            };
        });

        services.AddAuthorization();

        return services;
    }
}
