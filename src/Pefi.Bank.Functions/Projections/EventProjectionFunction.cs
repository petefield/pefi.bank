using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Pefi.Bank.Infrastructure;
using Pefi.Bank.Infrastructure.EventStore;
using Pefi.Bank.Infrastructure.Serialization;

namespace Pefi.Bank.Functions.Projections;

public class EventProjectionFunction(
    IEnumerable<IProjectionHandler> projectionHandlers,
    IEnumerable<ISagaExecutor> sagaExecutors,
    ILogger<EventProjectionFunction> logger)
{
    [Function("ProjectEvents")]
    public async Task Run(
        [CosmosDBTrigger(
            databaseName: "pefibank",
            containerName: "events",
            Connection = "CosmosDBConnection",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<EventDocument> documents)
    {
        foreach (var doc in documents)
        {
            try
            {
                // Restore trace context from the event document so that
                // projections and saga steps appear under the trace that
                // originally wrote the event (full saga lifetime in one trace).
                ActivityContext parentContext = default;
                if (doc.TraceId is not null && doc.SpanId is not null)
                {
                    parentContext = new ActivityContext(
                        ActivityTraceId.CreateFromString(doc.TraceId),
                        ActivitySpanId.CreateFromString(doc.SpanId),
                        ActivityTraceFlags.Recorded);
                }

                using var activity = DiagnosticConfig.Source.StartActivity(
                    "ChangeFeedHandler.Invoked",
                    ActivityKind.Consumer,
                    parentContext);
                activity?.SetTag("pefi.event_type", doc.EventType);
                activity?.SetTag("pefi.stream_id", doc.StreamId);

                var @event = EventSerializer.Deserialize(doc.EventType, doc.Data);

                // Run projection handlers
                foreach (var handler in projectionHandlers)
                {
                    if (handler.CanHandle(doc.EventType))
                    {
                        using var handlerActivity = DiagnosticConfig.Source.StartActivity("Projection.Handle");
                        handlerActivity?.SetTag("pefi.handler", handler.GetType().Name);
                        handlerActivity?.SetTag("pefi.event_type", doc.EventType);

                        await handler.HandleAsync(@event);
                        DiagnosticConfig.EventsProjected.Add(1,
                            new KeyValuePair<string, object?>("pefi.event_type", doc.EventType),
                            new KeyValuePair<string, object?>("pefi.handler", handler.GetType().Name));

                        logger.LogInformation("Projected event {EventType} for stream {StreamId}", doc.EventType, doc.StreamId);
                    }
                }

                // Run saga executors
                foreach (var saga in sagaExecutors)
                {
                    if (saga.CanHandle(doc.EventType))
                    {
                        using var handlerActivity = DiagnosticConfig.Source.StartActivity("Saga.Handle");
                        handlerActivity?.SetTag("pefi.handler", saga.GetType().Name);
                        handlerActivity?.SetTag("pefi.event_type", doc.EventType);
                        await saga.HandleAsync(doc);
                    }
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to project event {EventType} for stream {StreamId}",
                    doc.EventType, doc.StreamId);
            }
        }
    }
}
