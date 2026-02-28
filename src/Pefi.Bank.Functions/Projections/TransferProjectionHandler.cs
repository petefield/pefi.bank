using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Events;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Functions.Projections;

public class TransferProjectionHandler(
    IReadStore readStore,
    EventNotificationPublisher notificationPublisher) : IProjectionHandler
{

    private static readonly HashSet<string> HandledEvents =
    [
        "TransferInitiated", "TransferSourceDebited", "TransferDestinationCredited",
        "TransferSourceDebitCompensated", "TransferCompleted", "TransferFailed"
    ];

    public bool CanHandle(string eventType) => HandledEvents.Contains(eventType);


    public async Task HandleAsync(DomainEvent @event)
    {
        await (@event switch
        {
            TransferInitiated e => HandleInitiatedAsync(e), 
            TransferSourceDebited e => UpdateTransferStatus(e.TransferId, "SourceDebited", e.OccurredAt),
            TransferDestinationCredited e => UpdateTransferStatus(e.TransferId, "DestinationCredited", e.OccurredAt),
            TransferSourceDebitCompensated e => UpdateTransferStatus(e.TransferId, "SourceDebitCompensated", e.OccurredAt),
            TransferCompleted e => UpdateTransferStatus(e.TransferId, "Completed", e.OccurredAt),
            TransferFailed e => HandleFailedAsync(e),
            _ => Task.CompletedTask
        });
    }

    public async Task HandleInitiatedAsync(TransferInitiated @event)
    {
        var model = new TransferReadModel(        
            Id: @event.TransferId,
            SourceAccountId: @event.SourceAccountId,
            DestinationAccountId: @event.DestinationAccountId,
            Amount: @event.Amount,
            Description: @event.Description,
            Status: "Initiated",
            FailureReason: null,
            InitiatedAt: @event.OccurredAt,
            UpdatedAt: @event.OccurredAt,
            CompletedAt: null
        );

        await readStore.UpsertAsync(model, "transfer");
        await notificationPublisher.PublishAsync(new (@event.TransferId.ToString(), @event.EventType), model.PartitionKey);

    }

    public async Task HandleFailedAsync(TransferFailed @event)
    {
        var existing = await readStore.GetAsync<TransferReadModel>(@event.TransferId.ToString(), "transfer");

        if (existing is null)
            return;
        
        await readStore.UpsertAsync(existing with {
            Status = "Failed", 
            FailureReason = @event.Reason, 
            CompletedAt = @event.OccurredAt }, existing.PartitionKey);
        
        await notificationPublisher.PublishAsync(new (@event.TransferId.ToString(), @event.EventType), existing.PartitionKey);

    }

    private async Task UpdateTransferStatus(Guid transferId, string status, DateTime timestamp)
    {
        var existing = await readStore.GetAsync<TransferReadModel>(transferId.ToString(), "transfer");

        if (existing is  null)
            return;     
        
        await readStore.UpsertAsync(existing with { 
            Status = status,
            UpdatedAt = timestamp
        }, existing.PartitionKey);

        await notificationPublisher.PublishAsync(new (transferId.ToString(), "TransferStatusUpdated"), existing.PartitionKey);

    }
}
