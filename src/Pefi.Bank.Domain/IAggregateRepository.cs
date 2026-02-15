namespace Pefi.Bank.Domain;

public interface IAggregateRepository<T> where T : Aggregate, new()
{
    Task<T> LoadAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(T aggregate, CancellationToken ct = default);
}
