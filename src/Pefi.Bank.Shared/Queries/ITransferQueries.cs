using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Shared.Queries;

public interface ITransferQueries
{
    Task<TransferReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TransferReadModel>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
}
