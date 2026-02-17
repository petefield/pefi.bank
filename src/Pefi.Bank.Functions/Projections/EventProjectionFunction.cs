using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
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
                var @event = EventSerializer.Deserialize(doc.EventType, doc.Data);

                // Run projection handlers
                foreach (var handler in projectionHandlers)
                {
                    if (handler.CanHandle(doc.EventType))
                    {
                        await handler.HandleAsync(doc);
                    }
                }

                // Run saga executors
                foreach (var saga in sagaExecutors)
                {
                    if (saga.CanHandle(doc.EventType))
                    {
                        await saga.HandleAsync(doc);
                    }
                }

                logger.LogInformation("Projected event {EventType} for stream {StreamId}",
                    doc.EventType, doc.StreamId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to project event {EventType} for stream {StreamId}",
                    doc.EventType, doc.StreamId);
            }
        }
    }
}
