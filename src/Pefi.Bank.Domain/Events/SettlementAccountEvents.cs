namespace Pefi.Bank.Domain.Events;

public sealed record SettlementAccountCreated(
    Guid AccountId) : DomainEvent;

public sealed record SettlementCredited(
    Guid AccountId,
    decimal Amount,
    string Description) : DomainEvent;

public sealed record SettlementDebited(
    Guid AccountId,
    decimal Amount,
    string Description) : DomainEvent;
