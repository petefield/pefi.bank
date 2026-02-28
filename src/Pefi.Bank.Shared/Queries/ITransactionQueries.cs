using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Shared.Queries;

public interface ITransactionQueries
{
    Task<IReadOnlyList<TransactionReadModel>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
}
