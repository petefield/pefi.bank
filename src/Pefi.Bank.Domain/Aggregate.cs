namespace Pefi.Bank.Domain;

public abstract class Aggregate
{
    public Guid Id { get; protected set; }
    public int Version { get; private set; } = -1;

    private readonly List<IEvent> _uncommittedEvents = [];

    public IReadOnlyList<IEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    protected void RaiseEvent(IEvent @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    public void Load(IEnumerable<IEvent> history)
    {
        foreach (var @event in history)
        {
            Apply(@event);
            Version++;
        }
    }

    public void MarkCommitted()
    {
        Version += _uncommittedEvents.Count;
        _uncommittedEvents.Clear();
    }

    protected abstract void Apply(IEvent @event);
}
