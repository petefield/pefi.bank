using Pefi.Bank.Domain;
using Pefi.Bank.Infrastructure.ReadStore;

namespace Pefi.Bank.Infrastructure.EventStore;

public class AggregateRepository<T>(IEventStore eventStore ) : IAggregateRepository<T>
    where T : Aggregate, new()
{
    private static string StreamId(Guid id) => $"{typeof(T).Name}-{id}";

    public async Task<T> LoadAsync(Guid id, CancellationToken ct = default)
    {
        var streamId = StreamId(id);
        var events = await eventStore.LoadEventsAsync(streamId, ct);

        if (events.Count == 0)
            return new T(); // return empty aggregate if no events found

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
