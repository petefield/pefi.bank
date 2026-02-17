using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pefi.Bank.Api.Tests.Fakes;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Api.Tests;

public class CustomerEndpointTests : IClassFixture<BankApiFactory>
{
    private readonly HttpClient _client;
    private readonly BankApiFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CustomerEndpointTests(BankApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid CustomerId, HttpClient AuthClient)> RegisterCustomerAsync(
        string firstName = "Test", string lastName = "Customer")
    {
        var email = $"cust.{Guid.NewGuid():N}@example.com";
        var command = new RegisterCommand(firstName, lastName, email, "Password123!");
        var response = await _client.PostAsJsonAsync("/auth/register", command);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var authClient = _factory.CreateAuthenticatedClient(auth!.CustomerId, email);
        return (auth.CustomerId, authClient);
    }

    [Fact]
    public async Task CreateCustomer_ReturnsAccepted_WithIdAndEventsUrl()
    {
        var command = new CreateCustomerCommand("John", "Doe", "john@example.com");

        var response = await _client.PostAsJsonAsync("/customers", command);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out var idElement));
        Assert.True(Guid.TryParse(idElement.GetString(), out _));
        Assert.True(body.TryGetProperty("eventsUrl", out var eventsUrlElement));
        Assert.Contains("/customers/", eventsUrlElement.GetString());
    }

    [Fact]
    public async Task CreateCustomer_WithEmptyFirstName_ReturnsBadRequest()
    {
        var command = new CreateCustomerCommand("", "Doe", "john@example.com");

        var response = await _client.PostAsJsonAsync("/customers", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomer_WhenSeeded_ReturnsOk()
    {
        // Register so we get a valid customerId with auth
        var (customerId, authClient) = await RegisterCustomerAsync("Jane", "Smith");

        // Seed the read model with the same ID
        var model = new CustomerReadModel
        {
            Id = customerId,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            AccountCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _factory.ReadStore.Seed(customerId.ToString(), "customer", model);

        var response = await authClient.GetAsync($"/customers/{customerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerReadModel>(JsonOptions);
        Assert.NotNull(customer);
        Assert.Equal("Jane", customer.FirstName);
        Assert.Equal("Smith", customer.LastName);
    }

    [Fact]
    public async Task GetCustomer_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/customers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomer_DifferentCustomer_ReturnsForbid()
    {
        // Register as one customer, try to access another customer's data
        var (_, authClient) = await RegisterCustomerAsync();
        var otherCustomerId = Guid.NewGuid();

        var response = await authClient.GetAsync($"/customers/{otherCustomerId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCustomer_AfterRegister_ReturnsNoContent()
    {
        var (customerId, authClient) = await RegisterCustomerAsync("Alice", "Wonder");

        // Update
        var updateCommand = new UpdateCustomerCommand("Alice", "Wonderland", "alice.wonderland@example.com");
        var updateResponse = await authClient.PutAsJsonAsync($"/customers/{customerId}", updateCommand);

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCustomer_WithoutAuth_ReturnsUnauthorized()
    {
        var command = new UpdateCustomerCommand("Nobody", "Here", "nobody@example.com");

        var response = await _client.PutAsJsonAsync($"/customers/{Guid.NewGuid()}", command);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCustomer_DifferentCustomer_ReturnsForbid()
    {
        var (_, authClient) = await RegisterCustomerAsync();
        var otherCustomerId = Guid.NewGuid();

        var command = new UpdateCustomerCommand("Nobody", "Here", "nobody@example.com");
        var response = await authClient.PutAsJsonAsync($"/customers/{otherCustomerId}", command);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListCustomers_ReturnsOk()
    {
        var response = await _client.GetAsync("/customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
