using Microsoft.Azure.Cosmos;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Queries;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Infrastructure.Queries;

public class CosmosLedgerQueries(IReadStore readStore) : ILedgerQueries
{
    public async Task<IReadOnlyList<LedgerEntryReadModel>> ListAllAsync(CancellationToken ct = default)
    {
        return await readStore.QueryAsync<LedgerEntryReadModel>(
            new QueryDefinition(
                "SELECT * FROM c WHERE c.partitionKey = 'ledger' ORDER BY c.createdAt DESC"), ct);
    }

    public async Task<IReadOnlyList<LedgerEntryReadModel>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        return await readStore.QueryAsync<LedgerEntryReadModel>(
            new QueryDefinition(
                "SELECT * FROM c WHERE c.accountId = @accountId AND c.partitionKey = 'ledger' ORDER BY c.createdAt DESC")
                .WithParameter("@accountId", accountId.ToString()), ct);
    }
}
