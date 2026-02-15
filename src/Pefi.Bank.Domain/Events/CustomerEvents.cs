namespace Pefi.Bank.Domain.Events;

public sealed record CustomerCreated(
    Guid CustomerId,
    string FirstName,
    string LastName,
    string Email) : DomainEvent;

public sealed record CustomerUpdated(
    Guid CustomerId,
    string FirstName,
    string LastName,
    string Email) : DomainEvent;
