using System.Text.Json;
using Pefi.Bank.Infrastructure.EventStore;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Functions.Projections;

public class CustomerProjectionHandler(
    IReadStore readStore,
    EventNotificationPublisher notificationPublisher) : IProjectionHandler
{
    private static readonly HashSet<string> HandledEvents = ["CustomerCreated", "CustomerUpdated"];

    public bool CanHandle(string eventType) => HandledEvents.Contains(eventType);

    public async Task HandleAsync(EventDocument doc)
    {
        switch (doc.EventType)
        {
            case "CustomerCreated":
                await ProjectCustomerCreated(doc);
                break;
            case "CustomerUpdated":
                await ProjectCustomerUpdated(doc);
                break;
        }

        await notificationPublisher.PublishAsync(doc);
    }

    private async Task ProjectCustomerCreated(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var model = new CustomerReadModel
        {
            Id = data.GetProperty("customerId").GetGuid(),
            FirstName = data.GetProperty("firstName").GetString()!,
            LastName = data.GetProperty("lastName").GetString()!,
            Email = data.GetProperty("email").GetString()!,
            AccountCount = 0,
            CreatedAt = doc.Timestamp,
            UpdatedAt = doc.Timestamp
        };

        await readStore.UpsertAsync(model, "customer");
    }

    private async Task ProjectCustomerUpdated(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var customerId = data.GetProperty("customerId").GetGuid();

        var existing = await readStore.GetAsync<CustomerReadModel>(customerId.ToString(), "customer");
        if (existing is null) return;

        existing.FirstName = data.GetProperty("firstName").GetString()!;
        existing.LastName = data.GetProperty("lastName").GetString()!;
        existing.Email = data.GetProperty("email").GetString()!;
        existing.UpdatedAt = doc.Timestamp;

        await readStore.UpsertAsync(existing, "customer");
    }
}
