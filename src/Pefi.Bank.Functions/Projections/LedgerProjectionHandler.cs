using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Events;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Functions.Projections;

public class LedgerProjectionHandler(IReadStore readStore) : IProjectionHandler
{
    public bool CanHandle(string eventType) => eventType == nameof(LedgerTransactionRecorded);

    public async Task HandleAsync(DomainEvent @event)
    {
        await (@event switch
        {
            LedgerTransactionRecorded e => HandleTransactionRecordedAsync(e),
            _ => Task.CompletedTask
        });
    }

    private async Task HandleTransactionRecordedAsync(LedgerTransactionRecorded e)
    {

        // Create debit entry
        var debitEntry = new LedgerEntryReadModel
        {
            Id = e.DebitEntryId,
            TransactionId = e.TransactionId,
            AccountId = e.DebitAccountId,
            EntryType = "Debit",
            Amount = e.Amount,
            Description = e.Description,
            TransactionType = e.TransactionType,
            CreatedAt = e.OccurredAt
        };
        await readStore.UpsertAsync(debitEntry, debitEntry.PartitionKey);

        // Create credit entry
        var creditEntry = new LedgerEntryReadModel
        {
            Id = e.CreditEntryId,
            TransactionId = e.TransactionId,
            AccountId = e.CreditAccountId,
            EntryType = "Credit",
            Amount = e.Amount,
            Description = e.Description,
            TransactionType = e.TransactionType,
            CreatedAt = e.OccurredAt
        };
        await readStore.UpsertAsync(creditEntry, creditEntry.PartitionKey);
    }
}
