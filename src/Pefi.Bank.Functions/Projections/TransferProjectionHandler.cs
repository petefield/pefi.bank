using System.Text.Json;
using Pefi.Bank.Infrastructure.EventStore;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Functions.Projections;

public class TransferProjectionHandler(
    IReadStore readStore,
    EventNotificationPublisher notificationPublisher) : IProjectionHandler
{
    private static readonly HashSet<string> NotifiableEvents = ["TransferCompleted", "TransferFailed"];

    private static readonly HashSet<string> HandledEvents =
    [
        "TransferInitiated", "TransferSourceDebited", "TransferDestinationCredited",
        "TransferSourceDebitCompensated", "TransferCompleted", "TransferFailed"
    ];

    public bool CanHandle(string eventType) => HandledEvents.Contains(eventType);

    public async Task HandleAsync(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var transferId = data.GetProperty("transferId").GetGuid();

        switch (doc.EventType)
        {
            case "TransferInitiated":
                var model = new TransferReadModel
                {
                    Id = transferId,
                    SourceAccountId = data.GetProperty("sourceAccountId").GetGuid(),
                    DestinationAccountId = data.GetProperty("destinationAccountId").GetGuid(),
                    Amount = data.GetProperty("amount").GetDecimal(),
                    Description = data.GetProperty("description").GetString()!,
                    Status = "Initiated",
                    InitiatedAt = doc.Timestamp
                };
                await readStore.UpsertAsync(model, "transfer");
                break;

            case "TransferSourceDebited":
                await UpdateTransferStatus(transferId, "SourceDebited");
                break;

            case "TransferDestinationCredited":
                await UpdateTransferStatus(transferId, "DestinationCredited");
                break;

            case "TransferSourceDebitCompensated":
                await UpdateTransferStatus(transferId, "SourceDebitCompensated");
                break;

            case "TransferCompleted":
                var completed = await readStore.GetAsync<TransferReadModel>(transferId.ToString(), "transfer");
                if (completed is not null)
                {
                    completed.Status = "Completed";
                    completed.CompletedAt = doc.Timestamp;
                    await readStore.UpsertAsync(completed, "transfer");
                }
                break;

            case "TransferFailed":
                var failed = await readStore.GetAsync<TransferReadModel>(transferId.ToString(), "transfer");
                if (failed is not null)
                {
                    failed.Status = "Failed";
                    failed.FailureReason = data.GetProperty("reason").GetString();
                    failed.CompletedAt = doc.Timestamp;
                    await readStore.UpsertAsync(failed, "transfer");
                }
                break;
        }

        if (NotifiableEvents.Contains(doc.EventType))
            await notificationPublisher.PublishAsync(doc);
    }

    private async Task UpdateTransferStatus(Guid transferId, string status)
    {
        var existing = await readStore.GetAsync<TransferReadModel>(transferId.ToString(), "transfer");
        if (existing is not null)
        {
            existing.Status = status;
            await readStore.UpsertAsync(existing, "transfer");
        }
    }
}
