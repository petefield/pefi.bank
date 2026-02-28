using Microsoft.Azure.Cosmos;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Queries;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Infrastructure.Queries;

public class CosmosAccountQueries(IReadStore readStore) : IAccountQueries
{
    public async Task<AccountReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await readStore.GetAsync<AccountReadModel>(id.ToString(), "account", ct);
    }

    public async Task<IReadOnlyList<AccountReadModel>> ListAllAsync(CancellationToken ct = default)
    {
        return await readStore.QueryAsync<AccountReadModel>(
            new QueryDefinition("SELECT * FROM c WHERE c.partitionKey = 'account'"), ct);
    }

    public async Task<IReadOnlyList<AccountReadModel>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
    {
        return await readStore.QueryAsync<AccountReadModel>(
            new QueryDefinition(
                "SELECT * FROM c WHERE c.customerId = @customerId AND c.partitionKey = 'account'")
                .WithParameter("@customerId", customerId.ToString()), ct);
    }
}
