using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.ReadModels;
using Microsoft.Azure.Cosmos;

namespace Pefi.Bank.Api.Endpoints;

public static class LedgerEndpoints
{
    public static void MapLedgerEndpoints(this WebApplication app)
    {
        app.MapGet("/ledger", GetLedgerEntries)
            .WithTags("Ledger")
            .WithName("GetLedgerEntries");

        app.MapGet("/settlement", GetSettlementAccount)
            .WithTags("Ledger")
            .WithName("GetSettlementAccount");
    }

    private static async Task<IResult> GetLedgerEntries(
        Guid? accountId,
        IReadStore readStore)
    {
        IReadOnlyList<LedgerEntryReadModel> entries;

        if (accountId.HasValue)
        {
            entries = await readStore.QueryAsync<LedgerEntryReadModel>(
                new QueryDefinition(
                    "SELECT * FROM c WHERE c.accountId = @accountId AND c.partitionKey = 'ledger' ORDER BY c.createdAt DESC")
                    .WithParameter("@accountId", accountId.Value.ToString()));
        }
        else
        {
            entries = await readStore.QueryAsync<LedgerEntryReadModel>(
                new QueryDefinition(
                    "SELECT * FROM c WHERE c.partitionKey = 'ledger' ORDER BY c.createdAt DESC"));
        }

        return Results.Ok(entries);
    }

    private static async Task<IResult> GetSettlementAccount(
        IReadStore readStore,
        IAggregateRepository<SettlementAccount> repository)
    {
        var model = await readStore.GetAsync<SettlementAccountReadModel>(
            SettlementAccount.WellKnownId.ToString(), "settlement");

        if (model is not null)
            return Results.Ok(model);

        // Fall back to event store
        var aggregate = await repository.LoadAsync(SettlementAccount.WellKnownId);
        if (aggregate.Version < 0)
        {
            // Settlement account hasn't been created yet — return zeroed model
            return Results.Ok(new SettlementAccountReadModel
            {
                Id = SettlementAccount.WellKnownId,
                Balance = 0,
                TotalDebits = 0,
                TotalCredits = 0,
                UpdatedAt = DateTime.UtcNow
            });
        }

        return Results.Ok(new SettlementAccountReadModel
        {
            Id = aggregate.Id,
            Balance = aggregate.Balance,
            TotalDebits = 0,
            TotalCredits = 0,
            UpdatedAt = DateTime.UtcNow
        });
    }
}
