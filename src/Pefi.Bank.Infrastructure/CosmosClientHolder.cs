using Microsoft.Azure.Cosmos;

namespace Pefi.Bank.Infrastructure;

/// <summary>
/// Wraps the application's CosmosClient so it is not registered directly
/// in DI as <see cref="CosmosClient"/>. This prevents the Azure Functions
/// CosmosDB trigger extension from discovering and reusing it for its
/// internal change-feed processor, which causes a builder-reuse error.
/// </summary>
public sealed class CosmosClientHolder(CosmosClient client) : IDisposable
{
    public CosmosClient Client { get; } = client;

    public void Dispose() => Client.Dispose();
}
