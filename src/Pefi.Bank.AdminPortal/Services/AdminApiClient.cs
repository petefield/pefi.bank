using System.Net.Http.Json;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.AdminPortal.Services;

public sealed class AdminApiClient
{
    private readonly HttpClient _http;

    public AdminApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<CustomerReadModel>> GetCustomersAsync()
    {
        return await _http.GetFromJsonAsync<List<CustomerReadModel>>("customers")
               ?? [];
    }

    public async Task<CustomerReadModel?> GetCustomerAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<CustomerReadModel>($"customers/{id}");
    }

    public async Task<List<AccountReadModel>> GetAccountsAsync()
    {
        return await _http.GetFromJsonAsync<List<AccountReadModel>>("accounts")
               ?? [];
    }

    public async Task<List<AccountReadModel>> GetCustomerAccountsAsync(Guid customerId)
    {
        return await _http.GetFromJsonAsync<List<AccountReadModel>>($"customers/{customerId}/accounts")
               ?? [];
    }

    public async Task<AccountReadModel?> GetAccountAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<AccountReadModel>($"accounts/{id}");
    }

    public async Task<List<TransactionReadModel>> GetAccountTransactionsAsync(Guid accountId)
    {
        return await _http.GetFromJsonAsync<List<TransactionReadModel>>($"accounts/{accountId}/transactions")
               ?? [];
    }

    public async Task<List<LedgerEntryReadModel>> GetLedgerEntriesAsync()
    {
        return await _http.GetFromJsonAsync<List<LedgerEntryReadModel>>("ledger")
               ?? [];
    }

    public async Task<List<LedgerEntryReadModel>> GetAccountLedgerEntriesAsync(Guid accountId)
    {
        return await _http.GetFromJsonAsync<List<LedgerEntryReadModel>>($"ledger?accountId={accountId}")
               ?? [];
    }

}
