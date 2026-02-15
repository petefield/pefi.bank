using System.Net;
using StackExchange.Redis;
using StackExchange.Redis.Maintenance;
using StackExchange.Redis.Profiling;

namespace Pefi.Bank.Api.Tests.Fakes;

/// <summary>
/// A minimal fake IConnectionMultiplexer for testing.
/// Returns a no-op subscriber that doesn't connect to any real Redis instance.
/// </summary>
public sealed class FakeConnectionMultiplexer : IConnectionMultiplexer
{
    public string ClientName => "FakeRedis";
    public string Configuration => "fake:6379";
    public int TimeoutMilliseconds => 5000;
    public long OperationCount => 0;
    public bool PreserveAsyncOrder { get => false; set { } }
    public bool IsConnected => true;
    public bool IsConnecting => false;
    public bool IncludeDetailInExceptions { get => false; set { } }
    public int StormLogThreshold { get => 0; set { } }

#pragma warning disable CS0067 // Events required by IConnectionMultiplexer but never raised in fake
    public event EventHandler<RedisErrorEventArgs>? ErrorMessage;
    public event EventHandler<ConnectionFailedEventArgs>? ConnectionFailed;
    public event EventHandler<InternalErrorEventArgs>? InternalError;
    public event EventHandler<ConnectionFailedEventArgs>? ConnectionRestored;
    public event EventHandler<EndPointEventArgs>? ConfigurationChanged;
    public event EventHandler<EndPointEventArgs>? ConfigurationChangedBroadcast;
    public event EventHandler<ServerMaintenanceEvent>? ServerMaintenanceEvent;
    public event EventHandler<HashSlotMovedEventArgs>? HashSlotMoved;
#pragma warning restore CS0067

    public void Close(bool allowCommandsToComplete = true) { }
    public Task CloseAsync(bool allowCommandsToComplete = true) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public void ExportConfiguration(Stream destination, ExportOptions options = ExportOptions.All) { }
    public ServerCounters GetCounters() => new(null);
    public IDatabase GetDatabase(int db = -1, object? asyncState = null) => null!;
    public EndPoint[] GetEndPoints(bool configuredOnly = false) => [];
    public int GetHashSlot(RedisKey key) => 0;
    public IServer GetServer(string host, int port, object? asyncState = null) => null!;
    public IServer GetServer(string hostAndPort, object? asyncState = null) => null!;
    public IServer GetServer(IPAddress host, int port) => null!;
    public IServer GetServer(EndPoint endpoint, object? asyncState = null) => null!;
    public IServer GetServer(RedisKey key, object? asyncState = null, CommandFlags flags = CommandFlags.None) => null!;
    public IServer[] GetServers() => [];
    public string GetStatus() => "Fake";
    public void GetStatus(TextWriter log) => log.Write("Fake");
    public string? GetStormLog() => null;
    public ISubscriber GetSubscriber(object? asyncState = null) => new FakeSubscriber();
    public int HashSlot(RedisKey key) => 0;
    public long PublishReconfigure(CommandFlags flags = CommandFlags.None) => 0;
    public Task<long> PublishReconfigureAsync(CommandFlags flags = CommandFlags.None) => Task.FromResult(0L);
    public void RegisterProfiler(Func<ProfilingSession?> profilingSessionProvider) { }
    public void ResetStormLog() { }
    public override string ToString() => "FakeConnectionMultiplexer";

    public Task<bool> ConfigureAsync(TextWriter? log = null) => Task.FromResult(true);
    public bool Configure(TextWriter? log = null) => true;

    public void AddLibraryNameSuffix(string suffix) { }

    public bool TryWait(Task task) => task.IsCompleted;
    public void Wait(Task task) => task.Wait();
    public T Wait<T>(Task<T> task) => task.Result;
    public void WaitAll(params Task[] tasks) => Task.WaitAll(tasks);
}

/// <summary>
/// A minimal fake ISubscriber that supports in-process pub/sub for testing.
/// </summary>
public sealed class FakeSubscriber : ISubscriber
{
    private readonly Dictionary<RedisChannel, Action<RedisChannel, RedisValue>> _handlers = new();

    public IConnectionMultiplexer Multiplexer => null!;

    public TimeSpan Ping(CommandFlags flags = CommandFlags.None) => TimeSpan.Zero;
    public Task<TimeSpan> PingAsync(CommandFlags flags = CommandFlags.None) => Task.FromResult(TimeSpan.Zero);

    public EndPoint? IdentifyEndpoint(RedisChannel channel, CommandFlags flags = CommandFlags.None) => null;
    public Task<EndPoint?> IdentifyEndpointAsync(RedisChannel channel, CommandFlags flags = CommandFlags.None) => Task.FromResult<EndPoint?>(null);

    public bool IsConnected(RedisChannel channel = default) => true;

    public long Publish(RedisChannel channel, RedisValue message, CommandFlags flags = CommandFlags.None)
    {
        if (_handlers.TryGetValue(channel, out var handler))
        {
            handler(channel, message);
            return 1;
        }
        return 0;
    }

    public async Task<long> PublishAsync(RedisChannel channel, RedisValue message, CommandFlags flags = CommandFlags.None)
    {
        return Publish(channel, message, flags);
    }

    public void Subscribe(RedisChannel channel, Action<RedisChannel, RedisValue> handler, CommandFlags flags = CommandFlags.None)
    {
        _handlers[channel] = handler;
    }

    public Task SubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handler, CommandFlags flags = CommandFlags.None)
    {
        Subscribe(channel, handler, flags);
        return Task.CompletedTask;
    }

    public ChannelMessageQueue Subscribe(RedisChannel channel, CommandFlags flags = CommandFlags.None) => null!;
    public Task<ChannelMessageQueue> SubscribeAsync(RedisChannel channel, CommandFlags flags = CommandFlags.None) => Task.FromResult<ChannelMessageQueue>(null!);

    public EndPoint SubscribedEndpoint(RedisChannel channel) => null!;

    public void Unsubscribe(RedisChannel channel, Action<RedisChannel, RedisValue>? handler = null, CommandFlags flags = CommandFlags.None)
    {
        _handlers.Remove(channel);
    }

    public Task UnsubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue>? handler = null, CommandFlags flags = CommandFlags.None)
    {
        Unsubscribe(channel, handler, flags);
        return Task.CompletedTask;
    }

    public void UnsubscribeAll(CommandFlags flags = CommandFlags.None)
    {
        _handlers.Clear();
    }

    public Task UnsubscribeAllAsync(CommandFlags flags = CommandFlags.None)
    {
        UnsubscribeAll(flags);
        return Task.CompletedTask;
    }

    public bool TryWait(Task task) => task.IsCompleted;
    public void Wait(Task task) => task.Wait();
    public T Wait<T>(Task<T> task) => task.Result;
    public void WaitAll(params Task[] tasks) => Task.WaitAll(tasks);
}
