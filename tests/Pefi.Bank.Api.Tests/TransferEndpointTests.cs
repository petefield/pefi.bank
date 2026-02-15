using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pefi.Bank.Api.Tests.Fakes;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Api.Tests;

public class TransferEndpointTests : IClassFixture<BankApiFactory>
{
    private readonly HttpClient _client;
    private readonly BankApiFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TransferEndpointTests(BankApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateCustomerAsync()
    {
        var command = new CreateCustomerCommand("Transfer", "User", $"transfer{Guid.NewGuid():N}@example.com");
        var response = await _client.PostAsJsonAsync("/customers", command);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> OpenAndFundAccountAsync(Guid customerId, decimal initialBalance, string name = "Account")
    {
        var openCommand = new OpenAccountCommand(name);
        var openResponse = await _client.PostAsJsonAsync($"/accounts?customerId={customerId}", openCommand);
        openResponse.EnsureSuccessStatusCode();
        var body = await openResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = Guid.Parse(body.GetProperty("id").GetString()!);

        if (initialBalance > 0)
        {
            var depositCommand = new DepositCommand(initialBalance, "Initial funding");
            var depositResponse = await _client.PostAsJsonAsync($"/accounts/{accountId}/deposit", depositCommand);
            depositResponse.EnsureSuccessStatusCode();
        }

        return accountId;
    }

    [Fact]
    public async Task InitiateTransfer_ReturnsCreated_WithCompletedStatus()
    {
        var customerId = await CreateCustomerAsync();
        var sourceId = await OpenAndFundAccountAsync(customerId, 1000.00m, "Source");
        var destId = await OpenAndFundAccountAsync(customerId, 0m, "Destination");

        var command = new TransferCommand(sourceId, destId, 250.00m, "Rent payment");
        var response = await _client.PostAsJsonAsync("/transfers", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out _));
        Assert.Equal("Completed", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task InitiateTransfer_InsufficientFunds_ReturnsCreated_WithFailedStatus()
    {
        var customerId = await CreateCustomerAsync();
        var sourceId = await OpenAndFundAccountAsync(customerId, 100.00m, "Low Balance");
        var destId = await OpenAndFundAccountAsync(customerId, 0m, "Destination");

        var command = new TransferCommand(sourceId, destId, 500.00m, "Too much");
        var response = await _client.PostAsJsonAsync("/transfers", command);

        // Transfer still gets created but with Failed status
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Failed", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task InitiateTransfer_SameAccount_ReturnsBadRequest()
    {
        var customerId = await CreateCustomerAsync();
        var accountId = await OpenAndFundAccountAsync(customerId, 500.00m, "Self Transfer");

        var command = new TransferCommand(accountId, accountId, 100.00m, "Self transfer");
        var response = await _client.PostAsJsonAsync("/transfers", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InitiateTransfer_ZeroAmount_ReturnsBadRequest()
    {
        var customerId = await CreateCustomerAsync();
        var sourceId = await OpenAndFundAccountAsync(customerId, 500.00m, "Source");
        var destId = await OpenAndFundAccountAsync(customerId, 0m, "Dest");

        var command = new TransferCommand(sourceId, destId, 0m, "Zero transfer");
        var response = await _client.PostAsJsonAsync("/transfers", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InitiateTransfer_SourceNotFound_ReturnsBadRequest()
    {
        var customerId = await CreateCustomerAsync();
        var destId = await OpenAndFundAccountAsync(customerId, 0m, "Dest");

        var command = new TransferCommand(Guid.NewGuid(), destId, 100.00m, "No source");
        var response = await _client.PostAsJsonAsync("/transfers", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InitiateTransfer_DestinationNotFound_ReturnsBadRequest()
    {
        var customerId = await CreateCustomerAsync();
        var sourceId = await OpenAndFundAccountAsync(customerId, 500.00m, "Source");

        var command = new TransferCommand(sourceId, Guid.NewGuid(), 100.00m, "No dest");
        var response = await _client.PostAsJsonAsync("/transfers", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTransfer_WhenSeeded_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var model = new TransferReadModel
        {
            Id = id,
            SourceAccountId = Guid.NewGuid(),
            DestinationAccountId = Guid.NewGuid(),
            Amount = 100.00m,
            Description = "Test transfer",
            Status = "Completed",
            InitiatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        _factory.ReadStore.Seed(id.ToString(), "transfer", model);

        var response = await _client.GetAsync($"/transfers/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transfer = await response.Content.ReadFromJsonAsync<TransferReadModel>(JsonOptions);
        Assert.NotNull(transfer);
        Assert.Equal(100.00m, transfer.Amount);
        Assert.Equal("Completed", transfer.Status);
    }

    [Fact]
    public async Task GetTransfer_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/transfers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
