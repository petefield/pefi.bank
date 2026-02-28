using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Shared.Queries;

public interface IAccountQueries
{
    Task<AccountReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AccountReadModel>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccountReadModel>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
}
