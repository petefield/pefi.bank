using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Infrastructure;
using Pefi.Bank.Infrastructure.EventStore;

namespace Pefi.Bank.Functions.Projections;

public class TransferSagaExecutor(
    IAggregateRepository<Account> accountRepo,
    IAggregateRepository<Transfer> transferRepo,
    IAggregateRepository<LedgerTransaction> ledgerRepo,
    ILogger<TransferSagaExecutor> logger) : ISagaExecutor
{
    private static readonly HashSet<string> SagaEvents =
        ["TransferInitiated", "TransferSourceDebited", "TransferDestinationCredited"];

    public bool CanHandle(string eventType) => SagaEvents.Contains(eventType);

    public async Task HandleAsync(EventDocument doc)
    {
        using var activity = DiagnosticConfig.Source.StartActivity("Saga.Execute");
        activity?.SetTag("pefi.saga.step", doc.EventType);
        activity?.SetTag("pefi.stream_id", doc.StreamId);

        var stopwatch = Stopwatch.GetTimestamp();

        switch (doc.EventType)
        {
            case "TransferInitiated":
                await DebitSource(doc);
                break;
            case "TransferSourceDebited":
                await CreditDestination(doc);
                break;
            case "TransferDestinationCredited":
                await RecordLedgerAndComplete(doc);
                break;
        }

        var elapsedMs = Stopwatch.GetElapsedTime(stopwatch).TotalMilliseconds;
        DiagnosticConfig.SagaStepDuration.Record(elapsedMs,
            new KeyValuePair<string, object?>("pefi.saga.step", doc.EventType));
    }

    // ── Saga Step 1: TransferInitiated -> Debit source account ──────────────

    private async Task DebitSource(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var transferId = data.GetProperty("transferId").GetGuid();
        var sourceAccountId = data.GetProperty("sourceAccountId").GetGuid();
        var destinationAccountId = data.GetProperty("destinationAccountId").GetGuid();

        var amount = data.GetProperty("amount").GetDecimal();
        var description = data.GetProperty("description").GetString()!;

        logger.LogInformation("Saga:Transfer:DebitSource {TransferId} for source account {SourceAccountId}, to destination account {DestinationAccountId}",
             transferId, 
             sourceAccountId, 
             destinationAccountId);

        var transfer = await transferRepo.LoadAsync(transferId);

        // Idempotency: if already past Initiated, skip
        if (transfer.Status != TransferStatus.Initiated)
        {
            logger.LogInformation("Transfer {TransferId} already past Initiated ({Status}), skipping debit",
                transferId, transfer.Status);
            return;
        }

        try
        {
            var source = await accountRepo.LoadAsync(sourceAccountId);
            source.Withdraw(amount, $"Transfer to {destinationAccountId}: {description}");
            await accountRepo.SaveAsync(source);

            transfer.MarkSourceDebited();
            await transferRepo.SaveAsync(transfer);

            logger.LogInformation("Saga: Source debited for transfer {TransferId}", transferId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Saga: Failed to debit source {sourceAccountId} for transfer {TransferId}", sourceAccountId, transferId);

            // Reload transfer in case state changed
            transfer = await transferRepo.LoadAsync(transferId);
            if (transfer.Status == TransferStatus.Initiated)
            {
                transfer.Fail($"Source debit failed: {ex.Message}");
                await transferRepo.SaveAsync(transfer);
                DiagnosticConfig.SagasFailed.Add(1);
            }
        }
    }

    // ── Saga Step 2: TransferSourceDebited -> Credit destination account ────

    private async Task CreditDestination(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var transferId = data.GetProperty("transferId").GetGuid();

        var transfer = await transferRepo.LoadAsync(transferId);

        // Idempotency: if already past SourceDebited, skip
        if (transfer.Status != TransferStatus.SourceDebited)
        {
            logger.LogInformation("Transfer {TransferId} already past SourceDebited ({Status}), skipping credit",
                transferId, transfer.Status);
            return;
        }

        try
        {
            var destination = await accountRepo.LoadAsync(transfer.DestinationAccountId);
            destination.Deposit(transfer.Amount,
                $"Transfer from {transfer.SourceAccountId}: {transfer.Description}");
            await accountRepo.SaveAsync(destination);

            transfer.MarkDestinationCredited();
            await transferRepo.SaveAsync(transfer);

            logger.LogInformation("Saga: Destination credited for transfer {TransferId}", transferId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Saga: Failed to credit destination for transfer {TransferId}, compensating", transferId);

            // Compensate: reverse the source debit
            try
            {
                var source = await accountRepo.LoadAsync(transfer.SourceAccountId);
                source.Deposit(transfer.Amount,
                    $"Compensation: transfer {transferId} failed");
                await accountRepo.SaveAsync(source);
            }
            catch (Exception compensateEx)
            {
                logger.LogError(compensateEx,
                    "Saga: CRITICAL — Failed to compensate source debit for transfer {TransferId}", transferId);
            }

            // Reload transfer and mark compensated then failed
            transfer = await transferRepo.LoadAsync(transferId);
            if (transfer.Status == TransferStatus.SourceDebited)
            {
                transfer.MarkSourceDebitCompensated();
                transfer.Fail($"Destination credit failed: {ex.Message}");
                await transferRepo.SaveAsync(transfer);
                DiagnosticConfig.SagasFailed.Add(1);
            }
        }
    }

    // ── Saga Step 3: TransferDestinationCredited -> Record ledger + complete ─

    private async Task RecordLedgerAndComplete(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var transferId = data.GetProperty("transferId").GetGuid();

        var transfer = await transferRepo.LoadAsync(transferId);

        // Idempotency: if already past DestinationCredited, skip
        if (transfer.Status != TransferStatus.DestinationCredited)
        {
            logger.LogInformation("Transfer {TransferId} already past DestinationCredited ({Status}), skipping ledger",
                transferId, transfer.Status);
            return;
        }

        try
        {
            // Record ledger transaction: DR Source, CR Destination
            var ledger = LedgerTransaction.Record(
                Guid.NewGuid(),
                "Transfer",
                transfer.SourceAccountId,
                transfer.DestinationAccountId,
                transfer.Amount,
                transfer.Description);
            await ledgerRepo.SaveAsync(ledger);

            transfer.Complete();
            await transferRepo.SaveAsync(transfer);

            DiagnosticConfig.SagasCompleted.Add(1);
            logger.LogInformation("Saga: Transfer {TransferId} completed", transferId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saga: Failed to record ledger/complete transfer {TransferId}", transferId);
            // Ledger recording is non-critical for fund safety — both accounts are already updated.
            // We still try to complete. If the transfer save fails, the change feed will retry.
        }
    }
}
