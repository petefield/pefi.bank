using System.Text.Json;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;
using Microsoft.Azure.Cosmos;
using StackExchange.Redis;
using Pefi.Bank.Api.Extensions;

namespace Pefi.Bank.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/accounts").WithTags("Accounts");

        group.MapPost("/", OpenAccount).WithName("OpenAccount");
        group.MapGet("/{id:guid}", GetAccount).WithName("GetAccount");
        group.MapPost("/{id:guid}/deposit", Deposit).WithName("Deposit");
        group.MapPost("/{id:guid}/withdraw", Withdraw).WithName("Withdraw");
        group.MapPost("/{id:guid}/close", CloseAccount).WithName("CloseAccount");
        group.MapGet("/{id:guid}/transactions", GetTransactions).WithName("GetTransactions");
        group.MapGet("/{id:guid}/events", SubscribeToAccountEvents).WithName("AccountEvents");

        // Nested under customers
        app.MapGet("/customers/{customerId:guid}/accounts", GetCustomerAccounts)
            .WithTags("Customers")
            .WithName("GetCustomerAccounts");
    }

    private static async Task<IResult> OpenAccount(
        OpenAccountCommand command,
        HttpRequest request,
        IAggregateRepository<Account> repository)
    {
        // CustomerId passed as query param or header for simplicity (no auth yet)
        if (!Guid.TryParse(request.Query["customerId"], out var customerId))
            return Results.BadRequest("customerId query parameter is required.");

        var accountId = Guid.NewGuid();
        var account = Account.Open(accountId, customerId, command.AccountName);
        await repository.SaveAsync(account);

        return Results.Accepted($"/accounts/{accountId}", new { id = accountId, eventsUrl = $"/accounts/{accountId}/events" });
    }

    private static async Task<IResult> GetAccount(
        Guid id,
        IReadStore readStore,
        IAggregateRepository<Account> repository)
    {
        var account = await readStore.GetAsync<AccountReadModel>(
            id.ToString(), "account");

        if (account is not null)
            return Results.Ok(account);

        // Fall back to event store when projection hasn't run yet
        var aggregate = await repository.LoadAsync(id);
        if (aggregate.Version < 0)
            return Results.NotFound();

        return Results.Ok(new AccountReadModel
        {
            Id = aggregate.Id,
            CustomerId = aggregate.CustomerId,
            AccountName = aggregate.AccountName,
            Balance = aggregate.Balance,
            IsClosed = aggregate.IsClosed,
            OpenedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private static async Task<IResult> Deposit(
        Guid id,
        DepositCommand command,
        IAggregateRepository<Account> repository)
    {
        var account = await repository.LoadAsync(id);
        if (account.Version < 0)
            return Results.NotFound();

        account.Deposit(command.Amount, command.Description);
        await repository.SaveAsync(account);

        return Results.NoContent();
    }

    private static async Task<IResult> Withdraw(
        Guid id,
        WithdrawCommand command,
        IAggregateRepository<Account> repository)
    {
        var account = await repository.LoadAsync(id);
        if (account.Version < 0)
            return Results.NotFound();

        account.Withdraw(command.Amount, command.Description);
        await repository.SaveAsync(account);

        return Results.NoContent();
    }

    private static async Task<IResult> CloseAccount(
        Guid id,
        IAggregateRepository<Account> repository)
    {
        var account = await repository.LoadAsync(id);
        if (account.Version < 0)
            return Results.NotFound();

        account.Close();
        await repository.SaveAsync(account);

        return Results.NoContent();
    }

    private static async Task<IResult> GetTransactions(
        Guid id,
        IReadStore readStore)
    {
        var transactions = await readStore.QueryAsync<TransactionReadModel>(
            new QueryDefinition(
                "SELECT * FROM c WHERE c.accountId = @accountId AND c.partitionKey = 'transaction' ORDER BY c.occurredAt DESC")
                .WithParameter("@accountId", id.ToString()));

        return Results.Ok(transactions);
    }

    private static async Task<IResult> GetCustomerAccounts(
        Guid customerId,
        IReadStore readStore)
    {
        var accounts = await readStore.QueryAsync<AccountReadModel>(
            new QueryDefinition(
                "SELECT * FROM c WHERE c.customerId = @customerId AND c.partitionKey = 'account'")
                .WithParameter("@customerId", customerId.ToString()));

        return Results.Ok(accounts);
    }

    private static async Task SubscribeToAccountEvents(
        Guid id,
        HttpContext context,
        IConnectionMultiplexer redis)
    {
        await redis.SubscribeToEvents(id, "account-events", context);
    }
}
