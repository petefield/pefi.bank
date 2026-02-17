namespace Pefi.Bank.Domain.Events;

public sealed record AccountOpened(
    Guid AccountId,
    Guid CustomerId,
    string AccountName,
    string AccountNumber,
    string SortCode) : DomainEvent;

public sealed record FundsDeposited(
    Guid AccountId,
    decimal Amount,
    string Description) : DomainEvent;

public sealed record FundsWithdrawn(
    Guid AccountId,
    decimal Amount,
    string Description) : DomainEvent;

public sealed record AccountClosed(
    Guid AccountId) : DomainEvent;
