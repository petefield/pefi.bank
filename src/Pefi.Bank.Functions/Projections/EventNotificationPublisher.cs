using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pefi.Bank.Domain.Messages;
using Pefi.Bank.Functions.Extensions;
using Pefi.Bank.Infrastructure.EventStore;
using StackExchange.Redis;

namespace Pefi.Bank.Functions.Projections;

public class EventNotificationPublisher(
    IConnectionMultiplexer redis,
    ILogger<EventNotificationPublisher> logger)
{
    public async Task PublishAsync(EventDocument doc)
    {
        string channel = string.Empty;
        try
        {
            var subscriber = redis.GetSubscriber();
            var (entityType, entityId) = doc.StreamId.ToEntityInfo();
            channel = $"{entityType}-events";

            var message = JsonSerializer.Serialize(new EntityStateChangedMessage { EntityId = entityId, State = doc.EventType });
            await subscriber.PublishAsync(RedisChannel.Literal($"{entityType}-events"), message);
            logger.LogInformation("Published state change: {EntityId} -> {State} on channel {Channel}", entityId, doc.EventType, channel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish state change to Redis on channel {Channel}", channel);
        }
    }
}
