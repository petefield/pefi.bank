using System.Security.Claims;
using Pefi.Bank.Domain;
using Pefi.Bank.Shared;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.Queries;
using StackExchange.Redis;
using Pefi.Bank.Api.Extensions;

namespace Pefi.Bank.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/accounts").WithTags("Accounts");

        group.MapPost("/", OpenAccount).WithName("OpenAccount").RequireAuthorization();
        group.MapGet("/", ListAccounts).WithName("ListAccounts");
        group.MapGet("/{id:guid}", GetAccount).WithName("GetAccount");
        group.MapPost("/{id:guid}/deposit", Deposit).WithName("Deposit").RequireAuthorization();
        group.MapPost("/{id:guid}/withdraw", Withdraw).WithName("Withdraw").RequireAuthorization();
        group.MapPost("/{id:guid}/close", CloseAccount).WithName("CloseAccount").RequireAuthorization();
        group.MapGet("/{id:guid}/transactions", GetTransactions).WithName("GetTransactions");
        group.MapGet("/{id:guid}/events", SubscribeToAccountEvents).WithName("AccountEvents");

        // Nested under customers
        app.MapGet("/customers/{customerId:guid}/accounts", GetCustomerAccounts)
            .WithTags("Customers")
            .WithName("GetCustomerAccounts")
            .RequireAuthorization();
    }

    private static Guid GetCustomerIdFromClaims(HttpContext context)
    {

        
        var claim = context.User.FindFirst("customerId")
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }

    private static async Task<IResult> OpenAccount(
        OpenAccountCommand command,
        HttpContext context,
        IAggregateRepository<Account> repository)
    {
        var customerId = GetCustomerIdFromClaims(context);

        var accountId = Guid.NewGuid();
        var account = Account.Open(accountId, customerId, command.AccountName);
        await repository.SaveAsync(account);

        return Results.Accepted($"/accounts/{accountId}", new { 
            id = accountId, 
            eventsUrl = $"/accounts/{accountId}/events" });
    }

    private static async Task<IResult> GetAccount(
        Guid id,
        IAccountQueries accountQueries)
    {
        var account = await accountQueries.GetByIdAsync(id);

        return account is not null
            ? Results.Ok(account)
            : Results.NotFound();
    }

    private static async Task<IResult> Deposit(
        Guid id,
        DepositCommand command,
        IAggregateRepository<Transfer> transferRepo)
    {
        var transferId = Guid.NewGuid();

        // Create the transfer — saga will handle the rest via change feed
        var transfer = Transfer.Initiate(
            transferId,
            WellKnownAccounts.SettlementAccountId,
            id,
            command.Amount,
            command.Description);

        await transferRepo.SaveAsync(transfer);

        return Results.Accepted($"/transfers/{transferId}", new { 
            id = transferId, 
            eventsUrl = $"/transfers/{transferId}/events" });
    }

    private static async Task<IResult> Withdraw(
        Guid id,
        WithdrawCommand command,
        IAggregateRepository<Transfer> transferRepo)
    {
        var transferId = Guid.NewGuid();

        // Create the transfer — saga will handle the rest via change feed
        var transfer = Transfer.Initiate(
            transferId,
            id,
            WellKnownAccounts.SettlementAccountId,
            command.Amount,
            command.Description);

        await transferRepo.SaveAsync(transfer);

        return Results.Accepted($"/transfers/{transferId}", new { 
            id = transferId, 
            eventsUrl = $"/transfers/{transferId}/events" });
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
        ITransactionQueries transactionQueries)
    {
        var transactions = await transactionQueries.GetByAccountIdAsync(id);
        return Results.Ok(transactions);
    }

    private static async Task<IResult> GetCustomerAccounts(
        Guid customerId,
        HttpContext context,
        IAccountQueries accountQueries)
    {
        // Verify the authenticated user owns this customer ID
        var authCustomerId = GetCustomerIdFromClaims(context);
        if (authCustomerId != customerId)
            return Results.Forbid();

        var accounts = await accountQueries.GetByCustomerIdAsync(customerId);
        return Results.Ok(accounts);
    }

    private static async Task<IResult> ListAccounts(IAccountQueries accountQueries)
    {
        var accounts = await accountQueries.ListAllAsync();
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
