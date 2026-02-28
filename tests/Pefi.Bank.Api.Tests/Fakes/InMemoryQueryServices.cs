using Pefi.Bank.Shared.Queries;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Api.Tests.Fakes;

public sealed class InMemoryAccountQueries(InMemoryReadStore readStore) : IAccountQueries
{
    public Task<AccountReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return readStore.GetAsync<AccountReadModel>(id.ToString(), "account", ct);
    }

    public async Task<IReadOnlyList<AccountReadModel>> ListAllAsync(CancellationToken ct = default)
    {
        var all = await readStore.GetAllOfType<AccountReadModel>("account");
        return all;
    }

    public async Task<IReadOnlyList<AccountReadModel>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
    {
        var all = await readStore.GetAllOfType<AccountReadModel>("account");
        return all.Where(a => a.CustomerId == customerId).ToList().AsReadOnly();
    }
}

public sealed class InMemoryTransactionQueries(InMemoryReadStore readStore) : ITransactionQueries
{
    public async Task<IReadOnlyList<TransactionReadModel>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        var all = await readStore.GetAllOfType<TransactionReadModel>("transaction");
        return all.Where(t => t.AccountId == accountId).OrderByDescending(t => t.OccurredAt).ToList().AsReadOnly();
    }
}

public sealed class InMemoryLedgerQueries(InMemoryReadStore readStore) : ILedgerQueries
{
    public async Task<IReadOnlyList<LedgerEntryReadModel>> ListAllAsync(CancellationToken ct = default)
    {
        var all = await readStore.GetAllOfType<LedgerEntryReadModel>("ledger");
        return all.OrderByDescending(e => e.CreatedAt).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<LedgerEntryReadModel>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        var all = await readStore.GetAllOfType<LedgerEntryReadModel>("ledger");
        return all.Where(e => e.AccountId == accountId).OrderByDescending(e => e.CreatedAt).ToList().AsReadOnly();
    }
}

public sealed class InMemoryCustomerQueries(InMemoryReadStore readStore) : ICustomerQueries
{
    public Task<CustomerReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return readStore.GetAsync<CustomerReadModel>(id.ToString(), "customer", ct);
    }

    public async Task<IReadOnlyList<CustomerReadModel>> ListAllAsync(CancellationToken ct = default)
    {
        var all = await readStore.GetAllOfType<CustomerReadModel>("customer");
        return all;
    }
}
