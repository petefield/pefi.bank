using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Pefi.Bank.Domain;
using Pefi.Bank.Infrastructure.EventStore;
using Pefi.Bank.Infrastructure.Queries;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Queries;

namespace Pefi.Bank.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCosmosInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string databaseName)
    {
        services.AddSingleton<CosmosClientHolder>(sp =>
        {
            var options = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                HttpClientFactory = () =>
                {
                    // Allow self-signed certs for local emulator
                    var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };
                    return new HttpClient(handler);
                },
                ConnectionMode = ConnectionMode.Gateway
            };

            return new CosmosClientHolder(new CosmosClient(connectionString, options));
        });

        services.AddSingleton<IEventStore>(sp =>
        {
            var client = sp.GetRequiredService<CosmosClientHolder>().Client;
            var container = client.GetContainer(databaseName, "events");
            return new CosmosEventStore(container);
        });

        services.AddSingleton<IReadStore>(sp =>
        {
            var client = sp.GetRequiredService<CosmosClientHolder>().Client;
            var container = client.GetContainer(databaseName, "readmodels");
            return new CosmosReadStore(container);
        });

        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));

        services.AddSingleton<IAccountQueries, CosmosAccountQueries>();
        services.AddSingleton<ITransactionQueries, CosmosTransactionQueries>();
        services.AddSingleton<ILedgerQueries, CosmosLedgerQueries>();
        services.AddSingleton<ICustomerQueries, CosmosCustomerQueries>();

        return services;
    }

    public static async Task InitializeCosmosAsync(this IServiceProvider services, string databaseName)
    {
        var holder = services.GetService<CosmosClientHolder>();
        if (holder is null)
            return; // Skip initialization when CosmosDB is not configured (e.g. tests)

        var client = holder.Client;

        var database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

        await database.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties("events", "/streamId")
            {
                UniqueKeyPolicy = new UniqueKeyPolicy
                {
                    UniqueKeys =
                    {
                        new UniqueKey { Paths = { "/streamId", "/version" } }
                    }
                }
            });

        await database.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties("readmodels", "/partitionKey"));

        await database.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties("users", "/id"));

        // Enable change feed on the events container — inherently supported by CosmosDB
    }
}
