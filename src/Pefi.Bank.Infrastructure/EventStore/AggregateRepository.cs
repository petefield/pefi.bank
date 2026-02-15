using Pefi.Bank.Domain;

namespace Pefi.Bank.Infrastructure.EventStore;

public class AggregateRepository<T>(IEventStore eventStore) : IAggregateRepository<T>
    where T : Aggregate, new()
{
    private static string StreamId(Guid id) => $"{typeof(T).Name}-{id}";

    public async Task<T> LoadAsync(Guid id, CancellationToken ct = default)
    {
        var events = await eventStore.LoadEventsAsync(StreamId(id), ct);

        var aggregate = new T();
        aggregate.Load(events);
        return aggregate;
    }

    public async Task SaveAsync(T aggregate, CancellationToken ct = default)
    {
        if (aggregate.UncommittedEvents.Count == 0)
            return;

        await eventStore.AppendEventsAsync(
            StreamId(aggregate.Id),
            aggregate.UncommittedEvents,
            aggregate.Version,
            ct);

        aggregate.MarkCommitted();
    }
}
