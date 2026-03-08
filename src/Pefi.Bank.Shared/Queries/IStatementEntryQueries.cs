using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Shared.Queries;

public interface IStatementEntryQueries
{
    Task<IReadOnlyList<StatementEntryReadModel>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
}
