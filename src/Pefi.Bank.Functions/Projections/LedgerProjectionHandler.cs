using System.Text.Json;
using Pefi.Bank.Infrastructure.EventStore;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Functions.Projections;

public class LedgerProjectionHandler(
    IReadStore readStore) : IProjectionHandler
{
    public bool CanHandle(string eventType) => eventType == "LedgerTransactionRecorded";

    public async Task HandleAsync(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var transactionId = data.GetProperty("transactionId").GetGuid();
        var transactionType = data.GetProperty("transactionType").GetString()!;
        var debitAccountId = data.GetProperty("debitAccountId").GetGuid();
        var creditAccountId = data.GetProperty("creditAccountId").GetGuid();
        var amount = data.GetProperty("amount").GetDecimal();
        var description = data.GetProperty("description").GetString()!;
        var debitEntryId = data.GetProperty("debitEntryId").GetGuid();
        var creditEntryId = data.GetProperty("creditEntryId").GetGuid();

        // Create debit entry
        var debitEntry = new LedgerEntryReadModel
        {
            Id = debitEntryId,
            TransactionId = transactionId,
            AccountId = debitAccountId,
            EntryType = "Debit",
            Amount = amount,
            Description = description,
            TransactionType = transactionType,
            CreatedAt = doc.Timestamp
        };
        await readStore.UpsertAsync(debitEntry, "ledger");

        // Create credit entry
        var creditEntry = new LedgerEntryReadModel
        {
            Id = creditEntryId,
            TransactionId = transactionId,
            AccountId = creditAccountId,
            EntryType = "Credit",
            Amount = amount,
            Description = description,
            TransactionType = transactionType,
            CreatedAt = doc.Timestamp
        };
        await readStore.UpsertAsync(creditEntry, "ledger");
    }
}
