using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Shared.Queries;

public interface ILedgerQueries
{
    Task<IReadOnlyList<LedgerEntryReadModel>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LedgerEntryReadModel>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
}
