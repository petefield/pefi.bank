using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pefi.Bank.Api.Tests.Fakes;
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

    private async Task<Guid> CreateCustomerAsync()
    {
        var command = new CreateCustomerCommand("Test", "Customer", $"test{Guid.NewGuid():N}@example.com");
        var response = await _client.PostAsJsonAsync("/customers", command);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> OpenAccountAsync(Guid customerId, string name = "Checking")
    {
        var command = new OpenAccountCommand(name);
        var response = await _client.PostAsJsonAsync($"/accounts?customerId={customerId}", command);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task OpenAccount_ReturnsAccepted_WithIdAndEventsUrl()
    {
        var customerId = await CreateCustomerAsync();
        var command = new OpenAccountCommand("Savings");

        var response = await _client.PostAsJsonAsync($"/accounts?customerId={customerId}", command);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out var idElement));
        Assert.True(Guid.TryParse(idElement.GetString(), out _));
        Assert.True(body.TryGetProperty("eventsUrl", out var eventsUrlElement));
        Assert.Contains("/accounts/", eventsUrlElement.GetString());
    }

    [Fact]
    public async Task OpenAccount_WithoutCustomerId_ReturnsBadRequest()
    {
        var command = new OpenAccountCommand("Savings");

        var response = await _client.PostAsJsonAsync("/accounts", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAccount_WhenSeeded_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var model = new AccountReadModel
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            AccountName = "Checking",
            Balance = 100.00m,
            IsClosed = false,
            OpenedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
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
    public async Task Deposit_ReturnsNoContent()
    {
        var customerId = await CreateCustomerAsync();
        var accountId = await OpenAccountAsync(customerId);

        var command = new DepositCommand(500.00m, "Initial deposit");
        var response = await _client.PostAsJsonAsync($"/accounts/{accountId}/deposit", command);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Deposit_ZeroAmount_ReturnsBadRequest()
    {
        var customerId = await CreateCustomerAsync();
        var accountId = await OpenAccountAsync(customerId);

        var command = new DepositCommand(0m, "Zero deposit");
        var response = await _client.PostAsJsonAsync($"/accounts/{accountId}/deposit", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Withdraw_AfterDeposit_ReturnsNoContent()
    {
        var customerId = await CreateCustomerAsync();
        var accountId = await OpenAccountAsync(customerId);

        // Deposit first
        var depositCommand = new DepositCommand(1000.00m, "Deposit");
        await _client.PostAsJsonAsync($"/accounts/{accountId}/deposit", depositCommand);

        // Then withdraw
        var withdrawCommand = new WithdrawCommand(200.00m, "Withdrawal");
        var response = await _client.PostAsJsonAsync($"/accounts/{accountId}/withdraw", withdrawCommand);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Withdraw_InsufficientFunds_ReturnsBadRequest()
    {
        var customerId = await CreateCustomerAsync();
        var accountId = await OpenAccountAsync(customerId);

        // Try to withdraw from empty account
        var command = new WithdrawCommand(100.00m, "Overdraft attempt");
        var response = await _client.PostAsJsonAsync($"/accounts/{accountId}/withdraw", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Insufficient funds", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CloseAccount_WithZeroBalance_ReturnsNoContent()
    {
        var customerId = await CreateCustomerAsync();
        var accountId = await OpenAccountAsync(customerId);

        var response = await _client.PostAsync($"/accounts/{accountId}/close", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CloseAccount_WithBalance_ReturnsBadRequest()
    {
        var customerId = await CreateCustomerAsync();
        var accountId = await OpenAccountAsync(customerId);

        // Deposit money first
        var depositCommand = new DepositCommand(100.00m, "Deposit");
        await _client.PostAsJsonAsync($"/accounts/{accountId}/deposit", depositCommand);

        // Try to close
        var response = await _client.PostAsync($"/accounts/{accountId}/close", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("non-zero balance", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CloseAccount_NotFound_Returns404()
    {
        var response = await _client.PostAsync($"/accounts/{Guid.NewGuid()}/close", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deposit_OnClosedAccount_ReturnsBadRequest()
    {
        var customerId = await CreateCustomerAsync();
        var accountId = await OpenAccountAsync(customerId);

        // Close the account
        await _client.PostAsync($"/accounts/{accountId}/close", null);

        // Try to deposit
        var command = new DepositCommand(100.00m, "Should fail");
        var response = await _client.PostAsJsonAsync($"/accounts/{accountId}/deposit", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("closed", body.GetProperty("error").GetString());
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
        var customerId = Guid.NewGuid();

        var response = await _client.GetAsync($"/customers/{customerId}/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
