using System.Collections.Concurrent;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Api.Tests.Fakes;

public sealed class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<string, List<IEvent>> _streams = new();
    private readonly ConcurrentDictionary<string, int> _versions = new();
    private readonly object _lock = new();

    public Task<IReadOnlyList<IEvent>> LoadEventsAsync(string streamId, CancellationToken ct = default)
    {
        if (_streams.TryGetValue(streamId, out var events))
            return Task.FromResult<IReadOnlyList<IEvent>>(events.AsReadOnly());

        return Task.FromResult<IReadOnlyList<IEvent>>(Array.Empty<IEvent>());
    }

    public Task AppendEventsAsync(string streamId, IReadOnlyList<IEvent> events, int expectedVersion, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var currentVersion = _versions.GetValueOrDefault(streamId, -1);

            if (currentVersion != expectedVersion)
                throw new ConcurrencyException(streamId, expectedVersion);

            if (!_streams.ContainsKey(streamId))
                _streams[streamId] = [];

            _streams[streamId].AddRange(events);
            _versions[streamId] = currentVersion + events.Count;
        }

        return Task.CompletedTask;
    }
}
