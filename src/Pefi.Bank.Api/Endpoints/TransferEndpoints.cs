using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Domain.Exceptions;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;
using Microsoft.Azure.Cosmos;

namespace Pefi.Bank.Api.Endpoints;

public static class TransferEndpoints
{
    public static void MapTransferEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/transfers").WithTags("Transfers");

        group.MapPost("/", InitiateTransfer).WithName("InitiateTransfer");
        group.MapGet("/{id:guid}", GetTransfer).WithName("GetTransfer");
    }

    private static async Task<IResult> InitiateTransfer(
        TransferCommand command,
        IAggregateRepository<Account> accountRepo,
        IAggregateRepository<Transfer> transferRepo)
    {
        var transferId = Guid.NewGuid();

        // Load both accounts
        var source = await accountRepo.LoadAsync(command.SourceAccountId);
        if (source.Version < 0)
            return Results.BadRequest("Source account not found.");

        var destination = await accountRepo.LoadAsync(command.DestinationAccountId);
        if (destination.Version < 0)
            return Results.BadRequest("Destination account not found.");

        // Create the transfer
        var transfer = Transfer.Initiate(
            transferId,
            command.SourceAccountId,
            command.DestinationAccountId,
            command.Amount,
            command.Description);

        try
        {
            // Withdraw from source
            source.Withdraw(command.Amount, $"Transfer to {command.DestinationAccountId}: {command.Description}");
            await accountRepo.SaveAsync(source);

            // Deposit to destination
            destination.Deposit(command.Amount, $"Transfer from {command.SourceAccountId}: {command.Description}");
            await accountRepo.SaveAsync(destination);

            // Mark transfer complete
            transfer.Complete();
        }
        catch (DomainException ex)
        {
            transfer.Fail(ex.Message);
        }

        await transferRepo.SaveAsync(transfer);

        return Results.Created($"/transfers/{transferId}", new { id = transferId, status = transfer.Status.ToString() });
    }

    private static async Task<IResult> GetTransfer(
        Guid id,
        IReadStore readStore,
        IAggregateRepository<Transfer> repository)
    {
        var transfer = await readStore.GetAsync<TransferReadModel>(
            id.ToString(), "transfer");

        if (transfer is not null)
            return Results.Ok(transfer);

        // Fall back to event store when projection hasn't run yet
        var aggregate = await repository.LoadAsync(id);
        if (aggregate.Version < 0)
            return Results.NotFound();

        return Results.Ok(new TransferReadModel
        {
            Id = aggregate.Id,
            SourceAccountId = aggregate.SourceAccountId,
            DestinationAccountId = aggregate.DestinationAccountId,
            Amount = aggregate.Amount,
            Description = aggregate.Description,
            Status = aggregate.Status.ToString(),
            FailureReason = aggregate.FailureReason,
            InitiatedAt = DateTime.UtcNow,
            CompletedAt = aggregate.Status == TransferStatus.Completed ? DateTime.UtcNow : null
        });
    }
}
