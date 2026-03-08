using Microsoft.Azure.Cosmos;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Queries;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Infrastructure.Queries;

public class CosmosStatementEntryQueries(IReadStore readStore) : IStatementEntryQueries
{
    public async Task<IReadOnlyList<StatementEntryReadModel>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        return await readStore.QueryAsync<StatementEntryReadModel>(
            new QueryDefinition(
                "SELECT * FROM c WHERE c.accountId = @accountId AND c.partitionKey = 'statement-entry' ORDER BY c.occurredAt DESC")
                .WithParameter("@accountId", accountId.ToString()), ct);
    }
}
