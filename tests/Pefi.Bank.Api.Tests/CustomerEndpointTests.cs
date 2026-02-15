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
        var id = Guid.NewGuid();
        var model = new CustomerReadModel
        {
            Id = id,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            AccountCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _factory.ReadStore.Seed(id.ToString(), "customer", model);

        var response = await _client.GetAsync($"/customers/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerReadModel>(JsonOptions);
        Assert.NotNull(customer);
        Assert.Equal("Jane", customer.FirstName);
        Assert.Equal("Smith", customer.LastName);
    }

    [Fact]
    public async Task GetCustomer_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/customers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCustomer_AfterCreate_ReturnsNoContent()
    {
        // First create the customer via API (events stored)
        var createCommand = new CreateCustomerCommand("Alice", "Wonder", "alice@example.com");
        var createResponse = await _client.PostAsJsonAsync("/customers", createCommand);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        var body = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        // Update
        var updateCommand = new UpdateCustomerCommand("Alice", "Wonderland", "alice.wonderland@example.com");
        var updateResponse = await _client.PutAsJsonAsync($"/customers/{id}", updateCommand);

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCustomer_NotFound_Returns404()
    {
        var command = new UpdateCustomerCommand("Nobody", "Here", "nobody@example.com");

        var response = await _client.PutAsJsonAsync($"/customers/{Guid.NewGuid()}", command);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListCustomers_ReturnsOk()
    {
        var response = await _client.GetAsync("/customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
