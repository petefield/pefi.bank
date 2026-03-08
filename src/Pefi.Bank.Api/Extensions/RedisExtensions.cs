using System.Text.Json;
using System.Threading.Channels;
using Pefi.Bank.Domain.Messages;
using StackExchange.Redis;

namespace Pefi.Bank.Api.Extensions;

public static class RedisExtensions
{
    public static async Task SubscribeToEvents(this IConnectionMultiplexer redis,
        Guid id,
        string channelName,
        HttpContext context)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        var subscriber = redis.GetSubscriber();
        var channel = RedisChannel.Literal(channelName);
        var ct = context.RequestAborted;

        var events = Channel.CreateUnbounded<string>();

        await subscriber.SubscribeAsync(channel, (_, message) =>
        {
            try
            {
                var messageStr = (string)message!;
                var msg = JsonSerializer.Deserialize<EntityStateChangedMessage>(messageStr);

                if (msg is not null && Guid.Parse(msg.EntityId) == id)
                {
                    events.Writer.TryWrite(messageStr);
                }
            }
            catch
            {
                // Ignore malformed messages
            }
        });

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            await foreach (var result in events.Reader.ReadAllAsync(timeoutCts.Token))
            {
                var Body = $"data: {result}\n\n";
                await context.Response.WriteAsync(Body, timeoutCts.Token);
                await context.Response.Body.FlushAsync(timeoutCts.Token);
                Console.WriteLine($"Sent event for entity {id} {Body} : {result}");
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or timeout — expected
        }
        finally
        {
            events.Writer.TryComplete();
            await subscriber.UnsubscribeAsync(channel);
        }
    }
}
