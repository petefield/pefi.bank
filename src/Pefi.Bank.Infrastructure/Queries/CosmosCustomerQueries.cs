using Microsoft.Azure.Cosmos;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Queries;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Infrastructure.Queries;

public class CosmosCustomerQueries(IReadStore readStore) : ICustomerQueries
{
    public async Task<CustomerReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await readStore.GetAsync<CustomerReadModel>(id.ToString(), "customer", ct);
    }

    public async Task<IReadOnlyList<CustomerReadModel>> ListAllAsync(CancellationToken ct = default)
    {
        return await readStore.QueryAsync<CustomerReadModel>(
            new QueryDefinition("SELECT * FROM c WHERE c.partitionKey = 'customer'"), ct);
    }
}
