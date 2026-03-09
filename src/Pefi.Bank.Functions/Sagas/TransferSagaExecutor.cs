using Microsoft.Extensions.Logging;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Domain.Events;
using Pefi.Bank.Infrastructure.EventStore;

namespace Pefi.Bank.Functions.Sagas;

public class TransferSagaExecutor(
    IAggregateRepository<Account> accountRepo,
    IAggregateRepository<Transfer> transferRepo,
    IAggregateRepository<LedgerTransaction> ledgerRepo,
    ILogger<TransferSagaExecutor> logger) : SagaExecutorBase<Transfer>(logger)
{

    protected override HashSet<string> SagaEvents => [
        nameof(TransferInitiated), 
        nameof(TransferSourceDebited), 
        nameof(TransferDestinationCredited) ];

    public override async Task<Transfer> GetSaga(Guid id) => await transferRepo.LoadAsync(id);

    public override async Task SaveSaga(Transfer saga) { 
                logger.LogInformation("Saving TransferSaga {SagaId} ", saga.Id);

        await transferRepo.SaveAsync(saga);
        
    }

    public override void MarkSagaFailed(Transfer transfer, string reason)
    {
        logger.LogInformation("Marking TransferSaga {SagaId} as failed: {Reason}", transfer.Id, reason);
        try
        {
            transfer.Fail(reason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while marking TransferSaga {SagaId} as failed: {ErrorMessage}", transfer.Id, ex.Message);
        }
    } 

    public override async Task HandleBase(DomainEvent @event, EventDocument document)
    {
        switch (@event)
        {
            // ── Step 1: Debit the source account ────────────────────────────
            case TransferInitiated e:

                await ExecuteStep(
                    eventId: e.TransferId,
                    stepName: "1/3 TransferInitiated -> Debit source",
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
                    stepName: "2/3 TransferSourceDebited -> Credit destination",
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
                    stepName: "3/3 TransferDestinationCredited -> Record ledger", 
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
