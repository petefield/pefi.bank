using System.Collections.Concurrent;
using Microsoft.Azure.Cosmos;
using Pefi.Bank.Infrastructure.ReadStore;

namespace Pefi.Bank.Api.Tests.Fakes;

public sealed class InMemoryReadStore : IReadStore
{
    private readonly ConcurrentDictionary<string, object> _items = new();

    private static string Key(string id, string partitionKey) => $"{partitionKey}:{id}";

    public Task<T?> GetAsync<T>(string id, string partitionKey, CancellationToken ct = default) where T : class
    {
        if (_items.TryGetValue(Key(id, partitionKey), out var item))
            return Task.FromResult((T?)item);

        return Task.FromResult<T?>(null);
    }

    public Task<IReadOnlyList<T>> QueryAsync<T>(string query, CancellationToken ct = default) where T : class
    {
        return QueryAsync<T>(new QueryDefinition(query), ct);
    }

    public Task<IReadOnlyList<T>> QueryAsync<T>(QueryDefinition query, CancellationToken ct = default) where T : class
    {
        // Return all items of the matching type — sufficient for integration tests
        var results = _items.Values.OfType<T>().ToList();
        return Task.FromResult<IReadOnlyList<T>>(results.AsReadOnly());
    }

    public Task UpsertAsync<T>(T item, string partitionKey, CancellationToken ct = default) where T : class
    {
        // Try to extract id via reflection for keying
        var idProp = typeof(T).GetProperty("Id");
        var id = idProp?.GetValue(item)?.ToString() ?? Guid.NewGuid().ToString();

        _items[Key(id, partitionKey)] = item;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, string partitionKey, CancellationToken ct = default)
    {
        _items.TryRemove(Key(id, partitionKey), out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Manually seed a read model for testing read endpoints.
    /// </summary>
    public void Seed<T>(string id, string partitionKey, T item) where T : class
    {
        _items[Key(id, partitionKey)] = item;
    }

    /// <summary>
    /// Returns all items of the given type within a partition key.
    /// Used by in-memory query service fakes for filtered queries.
    /// </summary>
    public Task<IReadOnlyList<T>> GetAllOfType<T>(string partitionKey) where T : class
    {
        var results = _items
            .Where(kvp => kvp.Key.StartsWith($"{partitionKey}:"))
            .Select(kvp => kvp.Value)
            .OfType<T>()
            .ToList();
        return Task.FromResult<IReadOnlyList<T>>(results.AsReadOnly());
    }
}
