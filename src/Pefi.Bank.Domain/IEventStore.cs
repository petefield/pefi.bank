namespace Pefi.Bank.Domain;

public interface IEventStore
{
    Task<IReadOnlyList<IEvent>> LoadEventsAsync(string streamId, CancellationToken ct = default);
    Task AppendEventsAsync(string streamId, IReadOnlyList<IEvent> events, int expectedVersion, CancellationToken ct = default);
}
