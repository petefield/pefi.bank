using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.CustomerPortal.Services;

public class BankApiClient
{
    private readonly HttpClient _http;

    public BankApiClient(HttpClient http)
    {
        _http = http;
    }

    // Customers
    public async Task<(Guid Id, string EventsUrl)> CreateCustomerAsync(CreateCustomerCommand command)
    {
        var response = await _http.PostAsJsonAsync("customers", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CustomerCreatedResponse>();
        return (result!.Id, result.EventsUrl);
    }

    public async Task<string?> WaitForCustomerEventAsync(string eventsUrl, CancellationToken ct = default)
    {
        try
        {
            using var stream = await _http.GetStreamAsync(eventsUrl, ct);
            await foreach (var item in SseParser.Create(stream).EnumerateAsync(ct))
            {
                return item.Data.ToString();
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }

    public async Task<CustomerReadModel?> GetCustomerAsync(Guid customerId)
    {
        return await _http.GetFromJsonAsync<CustomerReadModel>($"customers/{customerId}");
    }

    // Accounts
    public async Task<List<AccountReadModel>> GetAccountsAsync(Guid customerId)
    {
        return await _http.GetFromJsonAsync<List<AccountReadModel>>($"customers/{customerId}/accounts")
               ?? new List<AccountReadModel>();
    }

    public async Task<(Guid Id, string EventsUrl)> OpenAccountAsync(Guid customerId, OpenAccountCommand command)
    {
        var response = await _http.PostAsJsonAsync($"accounts?customerId={customerId}", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AccountCreatedResponse>();
        return (result!.Id, result.EventsUrl);
    }

    public async Task<string?> WaitForAccountEventAsync(string eventsUrl, CancellationToken ct = default)
    {
        try
        {
            using var stream = await _http.GetStreamAsync(eventsUrl, ct);
            await foreach (var item in SseParser.Create(stream).EnumerateAsync(ct))
            {
                return item.Data.ToString();
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }

    public async Task DepositAsync(Guid accountId, DepositCommand command)
    {
        var response = await _http.PostAsJsonAsync($"accounts/{accountId}/deposit", command);
        response.EnsureSuccessStatusCode();
    }

    public async Task WithdrawAsync(Guid accountId, WithdrawCommand command)
    {
        var response = await _http.PostAsJsonAsync($"accounts/{accountId}/withdraw", command);
        response.EnsureSuccessStatusCode();
    }

    // Transfers
    public async Task TransferAsync(TransferCommand command)
    {
        var response = await _http.PostAsJsonAsync("transfers", command);
        response.EnsureSuccessStatusCode();
    }

    // Transactions
    public async Task<List<TransactionReadModel>> GetTransactionsAsync(Guid accountId)
    {
        return await _http.GetFromJsonAsync<List<TransactionReadModel>>($"accounts/{accountId}/transactions")
               ?? new List<TransactionReadModel>();
    }

    private sealed record IdResponse(Guid Id);
    private sealed record CustomerCreatedResponse(Guid Id, string EventsUrl);
    private sealed record AccountCreatedResponse(Guid Id, string EventsUrl);
}
