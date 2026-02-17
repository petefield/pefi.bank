using System.Text.Json;
using Pefi.Bank.Infrastructure.EventStore;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.ReadModels;

namespace Pefi.Bank.Functions.Projections;

public class AccountProjectionHandler(
    IReadStore readStore,
    EventNotificationPublisher notificationPublisher) : IProjectionHandler
{
    private static readonly HashSet<string> NotifiableEvents = ["AccountOpened", "AccountClosed"];

    private static readonly HashSet<string> HandledEvents =
        ["AccountOpened", "FundsDeposited", "FundsWithdrawn", "AccountClosed"];

    public bool CanHandle(string eventType) => HandledEvents.Contains(eventType);

    public async Task HandleAsync(EventDocument doc)
    {
        switch (doc.EventType)
        {
            case "AccountOpened":
                await ProjectAccountOpened(doc);
                break;
            case "FundsDeposited":
            case "FundsWithdrawn":
                await ProjectBalanceChange(doc);
                break;
            case "AccountClosed":
                await ProjectAccountClosed(doc);
                break;
        }

        if (NotifiableEvents.Contains(doc.EventType))
            await notificationPublisher.PublishAsync(doc);
    }

    private async Task ProjectAccountOpened(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var customerId = data.GetProperty("customerId").GetGuid();

        var model = new AccountReadModel
        {
            Id = data.GetProperty("accountId").GetGuid(),
            CustomerId = customerId,
            AccountName = data.GetProperty("accountName").GetString()!,
            AccountNumber = data.GetProperty("accountNumber").GetString()!,
            SortCode = data.GetProperty("sortCode").GetString()!,
            Balance = 0,
            IsClosed = false,
            OpenedAt = doc.Timestamp,
            UpdatedAt = doc.Timestamp
        };

        await readStore.UpsertAsync(model, "account");

        // Update customer account count
        var customer = await readStore.GetAsync<CustomerReadModel>(customerId.ToString(), "customer");
        if (customer is not null)
        {
            customer.AccountCount++;
            customer.UpdatedAt = doc.Timestamp;
            await readStore.UpsertAsync(customer, "customer");
        }

        // Create transaction record
        await CreateTransaction(
            data.GetProperty("accountId").GetGuid(),
            "AccountOpened", 0, "Account opened", 0, doc.Timestamp);
    }

    private async Task ProjectBalanceChange(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var accountId = data.GetProperty("accountId").GetGuid();
        var amount = data.GetProperty("amount").GetDecimal();
        var description = data.GetProperty("description").GetString()!;

        var account = await readStore.GetAsync<AccountReadModel>(accountId.ToString(), "account");
        if (account is null) return;

        if (doc.EventType == "FundsDeposited")
            account.Balance += amount;
        else
            account.Balance -= amount;

        account.UpdatedAt = doc.Timestamp;
        await readStore.UpsertAsync(account, "account");

        await CreateTransaction(accountId, doc.EventType, amount, description, account.Balance, doc.Timestamp);
    }

    private async Task ProjectAccountClosed(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var accountId = data.GetProperty("accountId").GetGuid();

        var account = await readStore.GetAsync<AccountReadModel>(accountId.ToString(), "account");
        if (account is null) return;

        account.IsClosed = true;
        account.UpdatedAt = doc.Timestamp;
        await readStore.UpsertAsync(account, "account");
    }

    private async Task CreateTransaction(
        Guid accountId, string type, decimal amount, string description, decimal balanceAfter, DateTime occurredAt)
    {
        var transaction = new TransactionReadModel
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Type = type,
            Amount = amount,
            Description = description,
            BalanceAfter = balanceAfter,
            OccurredAt = occurredAt
        };

        await readStore.UpsertAsync(transaction, "transaction");
    }
}
