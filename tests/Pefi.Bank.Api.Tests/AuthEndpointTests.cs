using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pefi.Bank.Api.Tests.Fakes;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Api.Tests;

public class AuthEndpointTests : IClassFixture<BankApiFactory>
{
    private readonly HttpClient _client;
    private readonly BankApiFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AuthEndpointTests(BankApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ReturnsOk_WithTokenAndCustomerId()
    {
        var command = new RegisterCommand("John", "Doe", $"john.{Guid.NewGuid():N}@example.com", "Password123!");

        var response = await _client.PostAsJsonAsync("/auth/register", command);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.NotEqual(Guid.Empty, auth.CustomerId);
        Assert.Equal(command.Email, auth.Email);
        Assert.True(auth.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var email = $"duplicate.{Guid.NewGuid():N}@example.com";
        var command1 = new RegisterCommand("Jane", "Doe", email, "Password123!");
        var command2 = new RegisterCommand("Janet", "Smith", email, "Password456!");

        var response1 = await _client.PostAsJsonAsync("/auth/register", command1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var response2 = await _client.PostAsJsonAsync("/auth/register", command2);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Fact]
    public async Task Register_MissingFields_ReturnsBadRequest()
    {
        var command = new RegisterCommand("", "", "", "");

        var response = await _client.PostAsJsonAsync("/auth/register", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_ReturnsBadRequest()
    {
        var command = new RegisterCommand("Test", "User", $"short.{Guid.NewGuid():N}@example.com", "12345");

        var response = await _client.PostAsJsonAsync("/auth/register", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        var email = $"login.{Guid.NewGuid():N}@example.com";
        var password = "Password123!";

        // Register first
        var registerCmd = new RegisterCommand("Login", "User", email, password);
        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerCmd);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // Login
        var loginCmd = new LoginCommand(email, password);
        var response = await _client.PostAsJsonAsync("/auth/login", loginCmd);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.Equal(email, auth.Email);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var email = $"wrongpw.{Guid.NewGuid():N}@example.com";

        // Register
        var registerCmd = new RegisterCommand("Wrong", "Password", email, "CorrectPassword!");
        await _client.PostAsJsonAsync("/auth/register", registerCmd);

        // Login with wrong password
        var loginCmd = new LoginCommand(email, "WrongPassword!");
        var response = await _client.PostAsJsonAsync("/auth/login", loginCmd);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var loginCmd = new LoginCommand("nobody@example.com", "Password123!");

        var response = await _client.PostAsJsonAsync("/auth/login", loginCmd);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_MissingFields_ReturnsBadRequest()
    {
        var loginCmd = new LoginCommand("", "");

        var response = await _client.PostAsJsonAsync("/auth/login", loginCmd);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        // Try to open an account without authentication
        var command = new OpenAccountCommand("Savings");
        var response = await _client.PostAsJsonAsync("/accounts", command);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Succeeds()
    {
        // Register to get a valid token
        var email = $"protected.{Guid.NewGuid():N}@example.com";
        var registerCmd = new RegisterCommand("Protected", "User", email, "Password123!");
        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerCmd);
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // Use the token to open an account
        var authenticatedClient = _factory.CreateAuthenticatedClient(auth!.CustomerId, email);
        var command = new OpenAccountCommand("Savings");
        var response = await authenticatedClient.PostAsJsonAsync("/accounts", command);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
