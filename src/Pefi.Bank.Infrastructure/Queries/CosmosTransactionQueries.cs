using Microsoft.Azure.Cosmos;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Queries;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Infrastructure.Queries;

public class CosmosTransactionQueries(IReadStore readStore) : ITransactionQueries
{
    public async Task<IReadOnlyList<TransactionReadModel>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        return await readStore.QueryAsync<TransactionReadModel>(
            new QueryDefinition(
                "SELECT * FROM c WHERE c.accountId = @accountId AND c.partitionKey = 'transaction' ORDER BY c.occurredAt DESC")
                .WithParameter("@accountId", accountId.ToString()), ct);
    }
}
