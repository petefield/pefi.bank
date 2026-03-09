using Microsoft.Azure.Cosmos;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Queries;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Infrastructure.Queries;

public class CosmosTransferQueries(IReadStore readStore) : ITransferQueries
{
    public async Task<TransferReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await readStore.GetAsync<TransferReadModel>(id.ToString(), "transfer", ct);
    }

    public async Task<IReadOnlyList<TransferReadModel>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        return await readStore.QueryAsync<TransferReadModel>(
            new QueryDefinition(
                "SELECT * FROM c WHERE c.partitionKey = 'transfer' AND (c.sourceAccountId = @accountId OR c.destinationAccountId = @accountId)")
                .WithParameter("@accountId", accountId.ToString()), ct);
    }
}
