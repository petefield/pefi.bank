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

    private async Task<(Guid CustomerId, HttpClient AuthClient)> RegisterCustomerAsync()
    {
        var email = $"transfer.{Guid.NewGuid():N}@example.com";
        var command = new RegisterCommand("Transfer", "User", email, "Password123!");
        var response = await _client.PostAsJsonAsync("/auth/register", command);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var authClient = _factory.CreateAuthenticatedClient(auth!.CustomerId, email);
        return (auth.CustomerId, authClient);
    }

    private async Task<Guid> OpenAccountAsync(HttpClient authClient, string name = "Account")
    {
        var openCommand = new OpenAccountCommand(name);
        var openResponse = await authClient.PostAsJsonAsync("/accounts", openCommand);
        openResponse.EnsureSuccessStatusCode();
        var body = await openResponse.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task InitiateTransfer_ReturnsAccepted_WithInitiatedStatus()
    {
        var (_, authClient) = await RegisterCustomerAsync();
        var sourceId = await OpenAccountAsync(authClient, "Source");
        var destId = await OpenAccountAsync(authClient, "Destination");

        // Fund source account
        var depositCommand = new DepositCommand(1000.00m, "Initial funding");
        var depositResponse = await authClient.PostAsJsonAsync($"/accounts/{sourceId}/deposit", depositCommand);
        depositResponse.EnsureSuccessStatusCode();

        var command = new TransferCommand(sourceId, destId, 250.00m, "Rent payment");
        var response = await authClient.PostAsJsonAsync("/transfers", command);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out _));
        Assert.Equal("Initiated", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task InitiateTransfer_WithoutAuth_ReturnsUnauthorized()
    {
        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), 100.00m, "Unauth transfer");
        var response = await _client.PostAsJsonAsync("/transfers", command);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InitiateTransfer_SameAccount_ReturnsBadRequest()
    {
        var (_, authClient) = await RegisterCustomerAsync();
        var accountId = await OpenAccountAsync(authClient, "Self Transfer");

        var command = new TransferCommand(accountId, accountId, 100.00m, "Self transfer");
        var response = await authClient.PostAsJsonAsync("/transfers", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InitiateTransfer_ZeroAmount_ReturnsBadRequest()
    {
        var (_, authClient) = await RegisterCustomerAsync();
        var sourceId = await OpenAccountAsync(authClient, "Source");
        var destId = await OpenAccountAsync(authClient, "Dest");

        var command = new TransferCommand(sourceId, destId, 0m, "Zero transfer");
        var response = await authClient.PostAsJsonAsync("/transfers", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InitiateTransfer_NegativeAmount_ReturnsBadRequest()
    {
        var (_, authClient) = await RegisterCustomerAsync();
        var sourceId = await OpenAccountAsync(authClient, "Source");
        var destId = await OpenAccountAsync(authClient, "Dest");

        var command = new TransferCommand(sourceId, destId, -50m, "Negative transfer");
        var response = await authClient.PostAsJsonAsync("/transfers", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTransfer_WhenSeeded_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var model = new TransferReadModel
        (
            Id: id,
            SourceAccountId: Guid.NewGuid(),
            DestinationAccountId: Guid.NewGuid(),
            Amount: 100.00m,
            Description: "Test transfer",
            Status: "Completed",
            FailureReason: null,    
            InitiatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow
            
        );
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
