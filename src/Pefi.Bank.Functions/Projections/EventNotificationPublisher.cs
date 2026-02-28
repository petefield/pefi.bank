using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pefi.Bank.Domain.Messages;
using StackExchange.Redis;

namespace Pefi.Bank.Functions.Projections;

public class EventNotificationPublisher(
    IConnectionMultiplexer redis,
    ILogger<EventNotificationPublisher> logger)
{
    public async Task PublishAsync( EntityStateChangedMessage msg, string channel)
    {
        try
        {
            var subscriber = redis.GetSubscriber();


            var message = JsonSerializer.Serialize(msg);
            await subscriber.PublishAsync(RedisChannel.Literal($"{channel}-events"), message);
            logger.LogInformation("Published state change: {EntityId} -> {State} on channel {Channel}", msg.EntityId, msg.State, channel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish state change to Redis on channel {Channel}", channel);
        }
    }
}
