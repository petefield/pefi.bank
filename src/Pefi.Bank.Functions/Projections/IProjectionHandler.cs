using Pefi.Bank.Domain;

namespace Pefi.Bank.Functions.Projections;

public interface IProjectionHandler
{
    bool CanHandle(string eventType);
    Task HandleAsync(DomainEvent @event);
}
