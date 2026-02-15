using System.Text.Json;
using Pefi.Bank.Domain.Messages;
using StackExchange.Redis;

namespace   Pefi.Bank.Api.Extensions;

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

        var tcs = new TaskCompletionSource<string>();
        ct.Register(() => tcs.TrySetCanceled());

        await subscriber.SubscribeAsync(channel, (_, message) =>
        {
            try
            {
                var messageStr = (string)message!;
                var msg = JsonSerializer.Deserialize<EntityStateChangedMessage>(messageStr);

                if (msg is null) return;

                if (msg is not null && Guid.Parse(msg.EntityId) == id)
                {
                    tcs.TrySetResult(messageStr);
                }
            }
            catch
            {
                // Ignore malformed messages
            }
        });

        try
        {
            // Wait for a matching event or timeout (30 seconds)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
            timeoutCts.Token.Register(() => tcs.TrySetCanceled());

            var result = await tcs.Task;

            await context.Response.WriteAsync($"data: {result}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or timeout — expected
        }
        finally
        {
            await subscriber.UnsubscribeAsync(channel);
        }
    }
}