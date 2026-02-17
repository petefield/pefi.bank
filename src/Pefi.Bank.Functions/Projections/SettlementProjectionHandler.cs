using System.Text.Json;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Infrastructure.EventStore;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Functions.Projections;

public class SettlementProjectionHandler(
    IReadStore readStore) : IProjectionHandler
{
    private static readonly HashSet<string> HandledEvents =
        ["SettlementAccountCreated", "SettlementCredited", "SettlementDebited"];

    public bool CanHandle(string eventType) => HandledEvents.Contains(eventType);

    public async Task HandleAsync(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var accountId = SettlementAccount.WellKnownId;

        var existing = await readStore.GetAsync<SettlementAccountReadModel>(
            accountId.ToString(), "settlement");

        switch (doc.EventType)
        {
            case "SettlementAccountCreated":
                var model = existing ?? new SettlementAccountReadModel
                {
                    Id = accountId,
                    Balance = 0,
                    TotalDebits = 0,
                    TotalCredits = 0,
                    UpdatedAt = doc.Timestamp
                };
                await readStore.UpsertAsync(model, "settlement");
                break;

            case "SettlementDebited":
                if (existing is not null)
                {
                    var amount = data.GetProperty("amount").GetDecimal();
                    existing.Balance -= amount;
                    existing.TotalDebits += amount;
                    existing.UpdatedAt = doc.Timestamp;
                    await readStore.UpsertAsync(existing, "settlement");
                }
                break;

            case "SettlementCredited":
                if (existing is not null)
                {
                    var amount = data.GetProperty("amount").GetDecimal();
                    existing.Balance += amount;
                    existing.TotalCredits += amount;
                    existing.UpdatedAt = doc.Timestamp;
                    await readStore.UpsertAsync(existing, "settlement");
                }
                break;
        }
    }
}
