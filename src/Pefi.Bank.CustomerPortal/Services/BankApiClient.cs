using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.CustomerPortal.Services;

public sealed class BankApiClient(HttpClient http)
{
    // Auth
    public async Task<AuthResponse> RegisterAsync(RegisterCommand command)
    {
        var response = await http.PostAsJsonAsync("auth/register", command);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public async Task<AuthResponse?> LoginAsync(LoginCommand command)
    {
        var response = await http.PostAsJsonAsync("auth/login", command);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    // Customers
    public async Task<CustomerReadModel?> GetCustomerAsync(Guid customerId)
    {
        return await http.GetFromJsonAsync<CustomerReadModel>($"customers/{customerId}");
    }

    // Accounts
    public async Task<List<AccountReadModel>> GetAccountsAsync(Guid customerId)
    {
        return await http.GetFromJsonAsync<List<AccountReadModel>>($"customers/{customerId}/accounts")
               ?? [];
    }

    public async Task<(Guid Id, string EventsUrl)> OpenAccountAsync(OpenAccountCommand command)
    {
        var response = await http.PostAsJsonAsync("accounts", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AccountCreatedResponse>();
        return (result!.Id, result.EventsUrl);
    }

    public async Task<string?> WaitForAccountEventAsync(string eventsUrl, CancellationToken ct = default)
    {
        try
        {
            using var stream = await http.GetStreamAsync(eventsUrl, ct);
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
        var response = await http.PostAsJsonAsync($"accounts/{accountId}/deposit", command);
        response.EnsureSuccessStatusCode();
    }

    public async Task WithdrawAsync(Guid accountId, WithdrawCommand command)
    {
        var response = await http.PostAsJsonAsync($"accounts/{accountId}/withdraw", command);
        response.EnsureSuccessStatusCode();
    }

    // Transfers
    public async Task<TransferInitiatedResponse> TransferAsync(TransferCommand command)
    {
        var response = await http.PostAsJsonAsync("transfers", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TransferInitiatedResponse>();
        return result!;
    }

    public async IAsyncEnumerable<string> SubscribeToTransferEventsAsync(
        string eventsUrl,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var stream = await http.GetStreamAsync(eventsUrl, ct);
        await foreach (var item in SseParser.Create(stream).EnumerateAsync(ct))
        {
            yield return item.Data.ToString();
        }
    }

    public async Task<TransferReadModel?> GetTransferAsync(Guid transferId)
    {
        return await http.GetFromJsonAsync<TransferReadModel>($"transfers/{transferId}");
    }

    // Statement entries
    public async Task<List<StatementEntryReadModel>> GetStatementEntriesAsync(Guid accountId)
    {
        return await http.GetFromJsonAsync<List<StatementEntryReadModel>>($"accounts/{accountId}/transactions")
               ?? [];
    }

    private sealed record AccountCreatedResponse(Guid Id, string EventsUrl);
    public sealed record TransferInitiatedResponse(Guid Id, string Status, string EventsUrl);
}
