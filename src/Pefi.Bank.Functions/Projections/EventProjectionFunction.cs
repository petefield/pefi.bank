using System.Drawing;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Pefi.Bank.Functions.Extensions;
using Pefi.Bank.Infrastructure.EventStore;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Infrastructure.Serialization;
using Pefi.Bank.Shared.ReadModels;
using Pefi.Bank.Domain.Messages;
using StackExchange.Redis;

namespace Pefi.Bank.Functions.Projections;

public class EventProjectionFunction(
    IReadStore readStore,
    IConnectionMultiplexer redis,
    ILogger<EventProjectionFunction> logger)
{
    [Function("ProjectEvents")]
    public async Task Run(
        [CosmosDBTrigger(
            databaseName: "pefibank",
            containerName: "events",
            Connection = "CosmosDBConnection",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<EventDocument> documents)
    {
        foreach (var doc in documents)
        {
            try
            {
                var @event = EventSerializer.Deserialize(doc.EventType, doc.Data);

                switch (doc.EventType)
                {
                    case "CustomerCreated":
                        await ProjectCustomerCreated(doc);
                        await PublishEntityStateChanged(doc);
                        break;
                    case "CustomerUpdated":
                        await ProjectCustomerUpdated(doc);
                        await PublishEntityStateChanged(doc);

                        break;
                    case "AccountOpened":
                        await ProjectAccountOpened(doc);
                        await PublishEntityStateChanged(doc);
                        break;
                    case "FundsDeposited":
                    case "FundsWithdrawn":
                        await ProjectBalanceChange(doc);
                        break;
                    case "AccountClosed":
                        await ProjectAccountClosed(doc);
                        await PublishEntityStateChanged(doc);

                        break;
                    case "TransferInitiated":
                    case "TransferCompleted":
                    case "TransferFailed":
                        await ProjectTransfer(doc);
                        break;
                }

                logger.LogInformation("Projected event {EventType} for stream {StreamId}",
                    doc.EventType, doc.StreamId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to project event {EventType} for stream {StreamId}",
                    doc.EventType, doc.StreamId);
            }
        }
    }

    private async Task ProjectCustomerCreated(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var model = new CustomerReadModel
        {
            Id = data.GetProperty("customerId").GetGuid(),
            FirstName = data.GetProperty("firstName").GetString()!,
            LastName = data.GetProperty("lastName").GetString()!,
            Email = data.GetProperty("email").GetString()!,
            AccountCount = 0,
            CreatedAt = doc.Timestamp,
            UpdatedAt = doc.Timestamp
        };

        await readStore.UpsertAsync(model, "customer");
    }

    private async Task ProjectCustomerUpdated(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var customerId = data.GetProperty("customerId").GetGuid();

        var existing = await readStore.GetAsync<CustomerReadModel>(customerId.ToString(), "customer");
        if (existing is null) return;

        existing.FirstName = data.GetProperty("firstName").GetString()!;
        existing.LastName = data.GetProperty("lastName").GetString()!;
        existing.Email = data.GetProperty("email").GetString()!;
        existing.UpdatedAt = doc.Timestamp;

        await readStore.UpsertAsync(existing, "customer");
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

    private async Task ProjectTransfer(EventDocument doc)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(doc.Data);
        var transferId = data.GetProperty("transferId").GetGuid();

        switch (doc.EventType)
        {
            case "TransferInitiated":
                var model = new TransferReadModel
                {
                    Id = transferId,
                    SourceAccountId = data.GetProperty("sourceAccountId").GetGuid(),
                    DestinationAccountId = data.GetProperty("destinationAccountId").GetGuid(),
                    Amount = data.GetProperty("amount").GetDecimal(),
                    Description = data.GetProperty("description").GetString()!,
                    Status = "Initiated",
                    InitiatedAt = doc.Timestamp
                };
                await readStore.UpsertAsync(model, "transfer");
                break;

            case "TransferCompleted":
                var completed = await readStore.GetAsync<TransferReadModel>(transferId.ToString(), "transfer");
                if (completed is not null)
                {
                    completed.Status = "Completed";
                    completed.CompletedAt = doc.Timestamp;
                    await readStore.UpsertAsync(completed, "transfer");
                }
                break;

            case "TransferFailed":
                var failed = await readStore.GetAsync<TransferReadModel>(transferId.ToString(), "transfer");
                if (failed is not null)
                {
                    failed.Status = "Failed";
                    failed.FailureReason = data.GetProperty("reason").GetString();
                    failed.CompletedAt = doc.Timestamp;
                    await readStore.UpsertAsync(failed, "transfer");
                }
                break;
        }
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

    private async Task PublishEntityStateChanged(EventDocument doc)
    {
        string channel = string.Empty;
        try
        {
            var subscriber = redis.GetSubscriber();
            var (entityType, entityId) = doc.StreamId.ToEntityInfo();
            channel = $"{entityType}-events";

            var message = JsonSerializer.Serialize(new EntityStateChangedMessage { EntityId = entityId, State = doc.EventType });
            await subscriber.PublishAsync(RedisChannel.Literal($"{entityType}-events"), message);
            logger.LogInformation("Published state change: {EntityId} -> {State} on channel {Channel}", entityId, doc.EventType, $"{entityType}-events");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish state change to Redis on channel {Channel}", channel);
        }
    }

}
