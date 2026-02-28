using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Events;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Functions.Projections;

public class CustomerProjectionHandler(
    IReadStore readStore,
    EventNotificationPublisher notificationPublisher) : IProjectionHandler
{

    private static readonly HashSet<string> HandledEvents = [nameof(CustomerCreated), nameof(CustomerUpdated)];

    public bool CanHandle(string eventType) => HandledEvents.Contains(eventType);

    public async Task HandleAsync(DomainEvent @event)
    {

        await (@event switch
        {
            CustomerCreated e => HandleCustomerCreated(e),
            CustomerUpdated e => HandleCustomerUpdated(e),
            _ => throw new InvalidOperationException($"Unsupported event type: {@event.GetType().Name}")
        });
    }

    private async Task HandleCustomerCreated(CustomerCreated evt)
    {
        
        var model = new CustomerReadModel
        {
            Id = evt.CustomerId,
            FirstName = evt.FirstName,
            LastName = evt.LastName,
            Email = evt.Email,
            AccountCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await readStore.UpsertAsync(model, model.PartitionKey);
        await notificationPublisher.PublishAsync(new ( evt.CustomerId.ToString(),  evt.EventType), model.PartitionKey);

    }

    private async Task HandleCustomerUpdated(CustomerUpdated? evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var existing = await readStore.GetAsync<CustomerReadModel>(evt.CustomerId.ToString(),  "customer");
        
        if (existing is null) 
            return;

        var customerReadModel = existing with
        {
            FirstName = evt.FirstName,
            LastName = evt.LastName,
            Email = evt.Email
        };

        await readStore.UpsertAsync(customerReadModel, existing.PartitionKey);
        await notificationPublisher.PublishAsync(new ( evt.CustomerId.ToString(), evt.EventType), existing.PartitionKey);
    }
}
