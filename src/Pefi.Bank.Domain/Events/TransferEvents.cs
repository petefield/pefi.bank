namespace Pefi.Bank.Domain.Events;

public sealed record TransferInitiated(
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Description) : DomainEvent;

public sealed record TransferSourceDebited(
    Guid TransferId) : DomainEvent;

public sealed record TransferDestinationCredited(
    Guid TransferId) : DomainEvent;

public sealed record TransferSourceDebitCompensated(
    Guid TransferId) : DomainEvent;

public sealed record TransferCompleted(
    Guid TransferId) : DomainEvent;

public sealed record TransferFailed(
    Guid TransferId,
    string Reason) : DomainEvent;
