namespace Pefi.Bank.Domain.Events;

public sealed record LedgerTransactionRecorded(
    Guid TransactionId,
    string TransactionType,
    Guid DebitAccountId,
    Guid CreditAccountId,
    decimal Amount,
    string Description,
    Guid DebitEntryId,
    Guid CreditEntryId) : DomainEvent;
