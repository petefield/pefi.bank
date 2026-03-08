using Microsoft.Extensions.Logging;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Events;
using Pefi.Bank.Infrastructure;
using Pefi.Bank.Infrastructure.EventStore;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Functions.Projections;

public abstract class ProjectionHandlerBase(ILogger logger) : IProjectionHandler
{
    protected abstract HashSet<string> HandlesEvents { get; }

    public bool CanHandle(string eventType) => HandlesEvents.Contains(eventType);

    protected abstract Task HandleInternalAsync(DomainEvent @event);

    public async Task HandleAsync(DomainEvent @event, EventDocument doc)
    {

        using var handlerActivity = DiagnosticConfig.Source.StartActivity("Projection.Handle");
        handlerActivity?.SetTag("pefi.handler", GetType().Name);
        handlerActivity?.SetTag("pefi.event_type", doc.EventType);

        await HandleInternalAsync(@event);
        DiagnosticConfig.EventsProjected.Add(1,
            new KeyValuePair<string, object?>("pefi.event_type", doc.EventType),
            new KeyValuePair<string, object?>("pefi.handler", GetType().Name));

        logger.LogInformation("Completed Project event {EventType} for stream {StreamId}", doc.EventType, doc.StreamId);


    }}
