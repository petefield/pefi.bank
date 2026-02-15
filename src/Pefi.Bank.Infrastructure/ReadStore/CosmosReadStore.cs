using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

namespace Pefi.Bank.Infrastructure.ReadStore;

public interface IReadStore
{
    Task<T?> GetAsync<T>(string id, string partitionKey, CancellationToken ct = default) where T : class;
    Task<IReadOnlyList<T>> QueryAsync<T>(string query, CancellationToken ct = default) where T : class;
    Task<IReadOnlyList<T>> QueryAsync<T>(QueryDefinition query, CancellationToken ct = default) where T : class;
    Task UpsertAsync<T>(T item, string partitionKey, CancellationToken ct = default) where T : class;
    Task DeleteAsync(string id, string partitionKey, CancellationToken ct = default);
}

public class CosmosReadStore(Container container) : IReadStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<T?> GetAsync<T>(string id, string partitionKey, CancellationToken ct = default) where T : class
    {
        try
        {
            var response = await container.ReadItemAsync<T>(id, new PartitionKey(partitionKey), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string query, CancellationToken ct = default) where T : class
    {
        return await QueryAsync<T>(new QueryDefinition(query), ct);
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(QueryDefinition query, CancellationToken ct = default) where T : class
    {
        var results = new List<T>();

        using var iterator = container.GetItemQueryIterator<T>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }

        return results.AsReadOnly();
    }

    public async Task UpsertAsync<T>(T item, string partitionKey, CancellationToken ct = default) where T : class
    {
        await container.UpsertItemAsync(item, new PartitionKey(partitionKey), cancellationToken: ct);
    }

    public async Task DeleteAsync(string id, string partitionKey, CancellationToken ct = default)
    {
        try
        {
            await container.DeleteItemAsync<object>(id, new PartitionKey(partitionKey), cancellationToken: ct);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already deleted, idempotent
        }
    }
}
