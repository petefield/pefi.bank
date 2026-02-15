namespace Pefi.Bank.Domain;

public interface IEvent
{
    string EventType { get; }
    DateTime OccurredAt { get; }
}
