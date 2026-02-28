using System.Security.Claims;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Api.Endpoints;

public static class TransferEndpoints
{
    public static void MapTransferEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/transfers").WithTags("Transfers");

        group.MapPost("/", InitiateTransfer).WithName("InitiateTransfer").RequireAuthorization();
        group.MapGet("/{id:guid}", GetTransfer).WithName("GetTransfer");
    }

    private static async Task<IResult> InitiateTransfer(
        TransferCommand command,
        HttpContext context,
        IAggregateRepository<Transfer> transferRepo,
        IAggregateRepository<Account> accountRepo)
    {
        // Verify the source account belongs to the authenticated customer
        var claim = context.User.FindFirst("customerId")
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier);
        var authCustomerId = Guid.Parse(claim!.Value);

        var sourceAccount = await accountRepo.LoadAsync(command.SourceAccountId);
        if (sourceAccount.Version < 0)
            return Results.NotFound(new { error = "Source account not found." });

        if (sourceAccount.CustomerId != authCustomerId)
            return Results.Forbid();

        var transferId = Guid.NewGuid();

        // Create the transfer — saga will handle the rest via change feed
        var transfer = Transfer.Initiate(
            transferId,
            command.SourceAccountId,
            command.DestinationAccountId,
            command.Amount,
            command.Description);

        await transferRepo.SaveAsync(transfer);

        return Results.Accepted(
            $"/transfers/{transferId}",
            new { id = transferId, status = transfer.Status.ToString() });
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

        return Results.Ok(new TransferReadModel(
            Id: aggregate.Id,
            SourceAccountId: aggregate.SourceAccountId,
            DestinationAccountId: aggregate.DestinationAccountId,
            Amount: aggregate.Amount,
            Description: aggregate.Description,
            Status: aggregate.Status.ToString(),
            FailureReason: aggregate.FailureReason,
            InitiatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CompletedAt: aggregate.Status == TransferStatus.Completed ? DateTime.UtcNow : null
        ));
    }
}
