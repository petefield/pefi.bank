using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pefi.Bank.Api.Tests.Fakes;
using Pefi.Bank.Domain.Events;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Api.Tests;

public class AccountEndpointTests : IClassFixture<BankApiFactory>
{
    private readonly HttpClient _client;
    private readonly BankApiFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AccountEndpointTests(BankApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid CustomerId, HttpClient AuthClient)> RegisterCustomerAsync()
    {
        var email = $"test.{Guid.NewGuid():N}@example.com";
        var command = new RegisterCommand("Test", "Customer", email, "Password123!");
        var response = await _client.PostAsJsonAsync("/auth/register", command);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var authClient = _factory.CreateAuthenticatedClient(auth!.CustomerId, email);
        return (auth.CustomerId, authClient);
    }

    private async Task<Guid> OpenAccountAsync(HttpClient authClient, string name = "Checking")
    {
        var command = new OpenAccountCommand(name);
        var response = await authClient.PostAsJsonAsync("/accounts", command);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task OpenAccount_ReturnsAccepted_WithIdAndEventsUrl()
    {
        var (_, authClient) = await RegisterCustomerAsync();
        var command = new OpenAccountCommand("Savings");

        var response = await authClient.PostAsJsonAsync("/accounts", command);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out var idElement));
        Assert.True(Guid.TryParse(idElement.GetString(), out _));
        Assert.True(body.TryGetProperty("eventsUrl", out var eventsUrlElement));
        Assert.Contains("/accounts/", eventsUrlElement.GetString());
    }

    [Fact]
    public async Task OpenAccount_WithoutAuth_ReturnsUnauthorized()
    {
        var command = new OpenAccountCommand("Savings");

        var response = await _client.PostAsJsonAsync("/accounts", command);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAccount_WhenSeeded_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var model = new AccountReadModel
        (
            Id: id,
            CustomerId : Guid.NewGuid(),
            AccountName : "Checking",
            AccountNumber : "12345678",
            SortCode : "12-34-56",
            Balance : 100.00m,
            IsClosed :false,
            OpenedAt : DateTime.UtcNow,
            UpdatedAt : DateTime.UtcNow,
            OverdraftLimit: 0
        );
        _factory.ReadStore.Seed(id.ToString(), "account", model);

        var response = await _client.GetAsync($"/accounts/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await response.Content.ReadFromJsonAsync<AccountReadModel>(JsonOptions);
        Assert.NotNull(account);
        Assert.Equal("Checking", account.AccountName);
        Assert.Equal(100.00m, account.Balance);
    }

    [Fact]
    public async Task GetAccount_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/accounts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deposit_ReturnsAccepted_WithTransferInfo()
    {
        var (_, authClient) = await RegisterCustomerAsync();
        var accountId = await OpenAccountAsync(authClient);

        var command = new DepositCommand(500.00m, "Initial deposit");
        var response = await authClient.PostAsJsonAsync($"/accounts/{accountId}/deposit", command);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out _));
        Assert.True(body.TryGetProperty("eventsUrl", out _));
    }

    [Fact]
    public async Task Deposit_ZeroAmount_ReturnsBadRequest()
    {
        var (_, authClient) = await RegisterCustomerAsync();
        var accountId = await OpenAccountAsync(authClient);

        var command = new DepositCommand(0m, "Zero deposit");
        var response = await authClient.PostAsJsonAsync($"/accounts/{accountId}/deposit", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Withdraw_ReturnsAccepted_WithTransferInfo()
    {
        var (_, authClient) = await RegisterCustomerAsync();
        var accountId = await OpenAccountAsync(authClient);

        var command = new WithdrawCommand(200.00m, "Withdrawal");
        var response = await authClient.PostAsJsonAsync($"/accounts/{accountId}/withdraw", command);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out _));
        Assert.True(body.TryGetProperty("eventsUrl", out _));
    }

    [Fact]
    public async Task CloseAccount_WithZeroBalance_ReturnsNoContent()
    {
        var (_, authClient) = await RegisterCustomerAsync();
        var accountId = await OpenAccountAsync(authClient);

        var response = await authClient.PostAsync($"/accounts/{accountId}/close", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CloseAccount_WithBalance_ReturnsBadRequest()
    {
        var (customerId, authClient) = await RegisterCustomerAsync();
        var accountId = await OpenAccountAsync(authClient);

        // Deposit directly via event store (deposit endpoint is async via saga)
        var depositEvent = new FundsDeposited(accountId, 100.00m, "Setup deposit");
        await _factory.EventStore.AppendEventsAsync($"Account-{accountId}", [depositEvent], 0);

        // Try to close — should fail because balance is non-zero
        var response = await authClient.PostAsync($"/accounts/{accountId}/close", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("non-zero balance", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CloseAccount_NotFound_Returns404()
    {
        var (_, authClient) = await RegisterCustomerAsync();

        var response = await authClient.PostAsync($"/accounts/{Guid.NewGuid()}/close", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactions_ReturnsOk()
    {
        var accountId = Guid.NewGuid();

        var response = await _client.GetAsync($"/accounts/{accountId}/transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomerAccounts_ReturnsOk()
    {
        var (customerId, authClient) = await RegisterCustomerAsync();

        var response = await authClient.GetAsync($"/customers/{customerId}/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomerAccounts_WithoutAuth_ReturnsUnauthorized()
    {
        var customerId = Guid.NewGuid();

        var response = await _client.GetAsync($"/customers/{customerId}/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
