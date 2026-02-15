namespace Pefi.Bank.Domain;

public abstract record DomainEvent : IEvent
{
    public string EventType => GetType().Name;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
