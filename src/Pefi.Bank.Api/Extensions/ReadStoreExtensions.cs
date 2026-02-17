using Pefi.Bank.Domain;
using Pefi.Bank.Infrastructure.ReadStore;

namespace Pefi.Bank.Api.Extensions;

public static class ReadStoreExtensions
{
    public static async Task<TReadModel?> ReadWithFallBack<TAggregate, TReadModel>(this IReadStore readStore,
        string streamName,
        Guid id, 
        IAggregateRepository<TAggregate> repository,
        Func<TAggregate,TReadModel> map, 
        CancellationToken ct = default)
        where TAggregate : Aggregate, new()
        where TReadModel : class, new()
    {
   
        var customer = await readStore.GetAsync<TReadModel>(id.ToString(), streamName,ct);

        if (customer is not null)
            return customer;    

        // Fall back to event store when projection hasn't run yet
        var aggregate = await repository.LoadAsync(id, ct);
        
        if (aggregate.Version < 0)
            return null;

        return map(aggregate);

    }
}
