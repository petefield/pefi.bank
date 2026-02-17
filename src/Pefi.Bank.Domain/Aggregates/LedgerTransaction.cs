using Pefi.Bank.Domain.Events;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Aggregates;

public class LedgerTransaction : Aggregate
{
    public string TransactionType { get; private set; } = string.Empty;
    public Guid DebitAccountId { get; private set; }
    public Guid CreditAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public LedgerEntry DebitEntry { get; private set; } = null!;
    public LedgerEntry CreditEntry { get; private set; } = null!;

    public static LedgerTransaction Record(
        Guid transactionId,
        string transactionType,
        Guid debitAccountId,
        Guid creditAccountId,
        decimal amount,
        string description)
    {
        if (amount <= 0)
            throw new DomainException("Ledger transaction amount must be positive.");

        if (debitAccountId == creditAccountId)
            throw new DomainException("Debit and credit accounts must be different.");

        ArgumentException.ThrowIfNullOrWhiteSpace(transactionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var transaction = new LedgerTransaction();
        transaction.RaiseEvent(new LedgerTransactionRecorded(
            transactionId,
            transactionType,
            debitAccountId,
            creditAccountId,
            amount,
            description,
            DebitEntryId: Guid.NewGuid(),
            CreditEntryId: Guid.NewGuid()));

        return transaction;
    }

    protected override void Apply(IEvent @event)
    {
        switch (@event)
        {
            case LedgerTransactionRecorded e:
                Id = e.TransactionId;
                TransactionType = e.TransactionType;
                DebitAccountId = e.DebitAccountId;
                CreditAccountId = e.CreditAccountId;
                Amount = e.Amount;
                Description = e.Description;
                DebitEntry = new LedgerEntry(
                    e.DebitEntryId, e.DebitAccountId, EntryType.Debit, e.Amount, e.Description);
                CreditEntry = new LedgerEntry(
                    e.CreditEntryId, e.CreditAccountId, EntryType.Credit, e.Amount, e.Description);
                break;

            default:
                throw new DomainException($"Unknown event type: {@event.GetType().Name}");
        }
    }
}
