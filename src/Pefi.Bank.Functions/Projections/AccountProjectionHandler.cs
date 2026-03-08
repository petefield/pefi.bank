using Microsoft.Extensions.Logging;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Events;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Functions.Projections;

public class AccountProjectionHandler(
    IReadStore readStore,
    EventNotificationPublisher notificationPublisher, ILogger<AccountProjectionHandler> logger) 
    : ProjectionHandlerBase(logger)
{
    protected override HashSet<string> HandlesEvents => [nameof(AccountOpened), nameof(FundsDeposited), nameof(FundsWithdrawn), nameof(AccountClosed)];

    protected override async Task HandleInternalAsync(DomainEvent @event)
    {
        await (@event switch
        {
            AccountOpened e => HandleAccountOpened(e),
            FundsDeposited e => HandleFundsDeposited(e),
            FundsWithdrawn e => HandleFundsWithdrawn(e),
            AccountClosed e => HandleAccountClosed(e),
            _ => throw new InvalidOperationException($"Unsupported event type: {@event.GetType().Name}")
        });
    }

    private async Task HandleAccountOpened(AccountOpened accountOpened)
    {
        var model = new AccountReadModel(
            accountOpened.AccountId,
            accountOpened.CustomerId,
            accountOpened.AccountName,
            accountOpened.AccountNumber,
            accountOpened.SortCode,
            0, // initial balance
            false, // not closed
            accountOpened.OccurredAt,
            accountOpened.OccurredAt,
            accountOpened.OverdraftLimit
        );

        await readStore.UpsertAsync(model, model.PartitionKey);

        // Update customer account count
        var customer = await readStore.GetAsync<CustomerReadModel>(accountOpened.CustomerId.ToString(), "customer");

        if (customer is not null)
        {
            customer.AccountCount++;
            customer.UpdatedAt = accountOpened.OccurredAt;
            await readStore.UpsertAsync(customer, customer.PartitionKey);
        }

        await notificationPublisher.PublishAsync(new(accountOpened.AccountId.ToString(), accountOpened.EventType), model.PartitionKey);
    }

    private async Task HandleFundsDeposited(FundsDeposited e) =>
        await HandleAccountBalanceChange(StatementEntryType.Credit, e.AccountId, e.Amount, e.Description, e.OccurredAt);
    
    private async Task HandleFundsWithdrawn(FundsWithdrawn e) =>
        await HandleAccountBalanceChange(StatementEntryType.Debit, e.AccountId, e.Amount, e.Description, e.OccurredAt);

    private async Task HandleAccountBalanceChange(StatementEntryType entryType, Guid accountId, decimal amount, string description, DateTime timestamp)
    {
        var account = await readStore.GetAsync<AccountReadModel>(accountId.ToString(), "account");
        if (account is null) return;

        var newAccountReadModel = account with
        {
            Balance = entryType == StatementEntryType.Credit ? account.Balance + amount : account.Balance - amount,
            UpdatedAt = timestamp
        };

        await readStore.UpsertAsync(newAccountReadModel, "account");

        await CreateStatementEntry(accountId, entryType, amount, description, newAccountReadModel.Balance, timestamp);
        await notificationPublisher.PublishAsync(new(accountId.ToString(), entryType.ToString()), "account");
    }

    private async Task HandleAccountClosed(AccountClosed e)
    {
        var account = await readStore.GetAsync<AccountReadModel>(e.AccountId.ToString(), "account");
        if (account is null) return;

        var newAccountReadModel = account with
        {
            IsClosed = true,
            UpdatedAt = e.OccurredAt
        };

        await readStore.UpsertAsync(newAccountReadModel, "account");
        await notificationPublisher.PublishAsync(new (e.AccountId.ToString(), nameof(AccountClosed)), "account");
    }

    private async Task CreateStatementEntry(
        Guid accountId, 
        StatementEntryType type, 
        decimal amount, 
        string description, 
        decimal balanceAfter, 
        DateTime occurredAt)
    {
        var entry = new StatementEntryReadModel
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Type = type,
            Amount = amount,
            Description = description,
            BalanceAfter = balanceAfter,
            OccurredAt = occurredAt
        };

        await readStore.UpsertAsync(entry, "statement-entry");
    }
}
