
using Pefi.Bank.Shared.Queries;

namespace Pefi.Bank.Api.Endpoints;

public static class LedgerEndpoints
{
    public static void MapLedgerEndpoints(this WebApplication app)
    {
        app.MapGet("/ledger", GetLedgerEntries)
            .WithTags("Ledger")
            .WithName("GetLedgerEntries");
    }

    private static async Task<IResult> GetLedgerEntries(
        Guid? accountId,
        ILedgerQueries ledgerQueries)
    {
        var entries = accountId.HasValue
            ? await ledgerQueries.GetByAccountIdAsync(accountId.Value)
            : await ledgerQueries.ListAllAsync();

        return Results.Ok(entries);
    }

}
