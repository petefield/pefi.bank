using Pefi.Bank.Infrastructure.EventStore;

namespace Pefi.Bank.Functions.Projections;

public interface IProjectionHandler
{
    bool CanHandle(string eventType);
    Task HandleAsync(EventDocument document);
}
