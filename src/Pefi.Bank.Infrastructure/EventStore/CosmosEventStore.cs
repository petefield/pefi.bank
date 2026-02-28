using System.Diagnostics;
using Microsoft.Azure.Cosmos;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Exceptions;
using Pefi.Bank.Infrastructure.Serialization;

namespace Pefi.Bank.Infrastructure.EventStore;

public class CosmosEventStore(Container container) : IEventStore
{
    public async Task<IReadOnlyList<IEvent>> LoadEventsAsync(string streamId, CancellationToken ct = default)
    {
        using var activity = DiagnosticConfig.Source.StartActivity("EventStore.LoadStream");
        activity?.SetTag("pefi.stream_id", streamId);

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.streamId = @streamId ORDER BY c.version")
            .WithParameter("@streamId", streamId);

        var events = new List<IEvent>();

        using var iterator = container.GetItemQueryIterator<EventDocument>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            foreach (var doc in response)
            {
                events.Add(EventSerializer.Deserialize(doc.EventType, doc.Data));
            }
        }

        activity?.SetTag("pefi.event_count", events.Count);
        return events.AsReadOnly();
    }

    public async Task AppendEventsAsync(
        string streamId,
        IReadOnlyList<IEvent> events,
        int expectedVersion,
        CancellationToken ct = default)
    {
        using var activity = DiagnosticConfig.Source.StartActivity("EventStore.AppendEvents");
        activity?.SetTag("pefi.stream_id", streamId);
        activity?.SetTag("pefi.event_count", events.Count);
        activity?.SetTag("pefi.expected_version", expectedVersion);
        activity?.SetTag("pefi.event_types", events.Select(e => e.EventType).Aggregate((a, b) => $"{a},{b}"));

        // Check current version for optimistic concurrency
        var currentVersion = await GetStreamVersionAsync(streamId, ct);
        if (currentVersion != expectedVersion)
            throw new ConcurrencyException(streamId, expectedVersion);

        var batch = container.CreateTransactionalBatch(new PartitionKey(streamId));

        for (var i = 0; i < events.Count; i++)
        {
            var @event = events[i];
            var version = expectedVersion + 1 + i;

            var document = new EventDocument
            {
                Id = $"{streamId}:{version}",
                StreamId = streamId,
                EventType = @event.EventType,
                Version = version,
                Data = EventSerializer.Serialize(@event),
                Timestamp = @event.OccurredAt,
                TraceId = Activity.Current?.TraceId.ToString(),
                SpanId = Activity.Current?.SpanId.ToString()
            };

            batch.CreateItem(document);
        }

        var result = await batch.ExecuteAsync(ct);
        if (!result.IsSuccessStatusCode)
        {
            activity?.SetStatus(ActivityStatusCode.Error, $"Batch failed: {result.StatusCode}");
            throw new InvalidOperationException($"Failed to append events to stream '{streamId}'. Status: {result.StatusCode}");
        }

        DiagnosticConfig.EventsStored.Add(events.Count,
            new KeyValuePair<string, object?>("pefi.stream_id", streamId));
    }

    private async Task<int> GetStreamVersionAsync(string streamId, CancellationToken ct)
    {
        var query = new QueryDefinition(
            "SELECT VALUE MAX(c.version) FROM c WHERE c.streamId = @streamId")
            .WithParameter("@streamId", streamId);

        using var iterator = container.GetItemQueryIterator<int?>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            return response.FirstOrDefault() ?? -1;
        }

        return -1;
    }
}
