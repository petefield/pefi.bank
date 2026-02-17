using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Pefi.Bank.Auth;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", Register).WithName("Register");
        group.MapPost("/login", Login).WithName("Login");
    }

    private static async Task<IResult> Register(
        RegisterCommand command,
        UserManager<ApplicationUser> userManager,
        JwtTokenService tokenService,
        IAggregateRepository<Customer> customerRepo)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(command.FirstName) ||
            string.IsNullOrWhiteSpace(command.LastName) ||
            string.IsNullOrWhiteSpace(command.Email) ||
            string.IsNullOrWhiteSpace(command.Password))
        {
            return Results.BadRequest(new { error = "All fields are required." });
        }

        // Check for existing user
        var existing = await userManager.FindByEmailAsync(command.Email);
        if (existing is not null)
        {
            return Results.Conflict(new { error = "An account with this email already exists." });
        }

        // Create the Identity user — use a new GUID that will also be the Customer aggregate ID
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId.ToString(),
            Email = command.Email,
            UserName = command.Email
        };

        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Results.BadRequest(new { error = errors });
        }

        // Create the Customer aggregate with the same ID
        var customer = Customer.Create(userId, command.FirstName, command.LastName, command.Email);
        await customerRepo.SaveAsync(customer);

        // Generate JWT
        var token = tokenService.GenerateToken(user);
        var expiry = tokenService.GetExpiry();

        return Results.Ok(new AuthResponse
        {
            Token = token,
            CustomerId = userId,
            Email = command.Email,
            ExpiresAt = expiry
        });
    }

    private static async Task<IResult> Login(
        LoginCommand command,
        UserManager<ApplicationUser> userManager,
        JwtTokenService tokenService)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return Results.BadRequest(new { error = "Email and password are required." });
        }

        var user = await userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var valid = await userManager.CheckPasswordAsync(user, command.Password);
        if (!valid)
        {
            return Results.Unauthorized();
        }

        var token = tokenService.GenerateToken(user);
        var expiry = tokenService.GetExpiry();

        return Results.Ok(new AuthResponse
        {
            Token = token,
            CustomerId = Guid.Parse(user.Id),
            Email = user.Email,
            ExpiresAt = expiry
        });
    }
}
