using Microsoft.Extensions.Logging;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Domain.Events;
using Pefi.Bank.Infrastructure.EventStore;

namespace Pefi.Bank.Functions.Projections;

public class TransferSagaExecutor(
    IAggregateRepository<Account> accountRepo,
    IAggregateRepository<Transfer> transferRepo,
    IAggregateRepository<LedgerTransaction> ledgerRepo,
    ILogger<TransferSagaExecutor> logger) : SagaExecutor<Transfer>(logger)
{

    protected override HashSet<string> SagaEvents => [
        nameof(TransferInitiated), 
        nameof(TransferSourceDebited), 
        nameof(TransferDestinationCredited) ];

    public override async Task<Transfer> GetSaga(Guid id) => await transferRepo.LoadAsync(id);

    public override async Task SaveSaga(Transfer saga) => await transferRepo.SaveAsync(saga);

    public override async Task HandleBase(DomainEvent @event, EventDocument document)
    {
        switch (@event)
        {
            // ── Step 1: Debit the source account ────────────────────────────
            case TransferInitiated e:
                logger.LogInformation(
                    "Transfer {TransferId} saga started — {Amount:C} from {SourceAccountId} to {DestinationAccountId} ({Description})",
                    e.TransferId, e.Amount, e.SourceAccountId, e.DestinationAccountId, e.Description);

                await ExecuteStep(
                    eventId: e.TransferId,
                    stepName: "1/3 Debit source",
                    execute: async (transfer) =>
                    {
                        var source = await accountRepo.LoadAsync(e.SourceAccountId);
                        source.Withdraw(e.Amount, $"Transfer to {e.DestinationAccountId}: {e.Description}");
                        await accountRepo.SaveAsync(source);

                        transfer.MarkSourceDebited();
                    });
                break;

            // ── Step 2: Credit the destination account (with compensation) ──
            case TransferSourceDebited e:
                await ExecuteStep(
                    eventId: e.TransferId, 
                    stepName: "2/3 Credit destination",
                    execute: async (transfer) =>
                    {
                        var destination = await accountRepo.LoadAsync(transfer.DestinationAccountId);
                        destination.Deposit(transfer.Amount, $"Transfer from {transfer.SourceAccountId}: {transfer.Description}");
                        await accountRepo.SaveAsync(destination);

                        transfer.MarkDestinationCredited();
                    },
                    compensate: async (transfer) =>
                    {
                        var source = await accountRepo.LoadAsync(transfer.SourceAccountId);
                        source.Deposit(transfer.Amount, $"Compensation: transfer {transfer.Id} failed");
                        await accountRepo.SaveAsync(source);

                        transfer.MarkSourceDebitCompensated();
                    });
                break;

            // ── Step 3: Record ledger entry and complete ────────────────────
            case TransferDestinationCredited e:
                await ExecuteStep(
                    eventId: e.TransferId, 
                    stepName: "3/3 Record ledger", 
                    execute: async (transfer) =>
                    {
                        var ledger = LedgerTransaction.Record(
                            Guid.NewGuid(),
                            "Transfer",
                            transfer.SourceAccountId,
                            transfer.DestinationAccountId,
                            transfer.Amount,
                            transfer.Description);
                        await ledgerRepo.SaveAsync(ledger);

                        transfer.Complete();
                    });
                break;
        }
    }
}
