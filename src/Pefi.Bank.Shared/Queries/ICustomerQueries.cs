using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Shared.Queries;

public interface ICustomerQueries
{
    Task<CustomerReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerReadModel>> ListAllAsync(CancellationToken ct = default);
}
