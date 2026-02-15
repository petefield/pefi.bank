using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pefi.Bank.Domain;
using Pefi.Bank.Infrastructure.ReadStore;
using StackExchange.Redis;

namespace Pefi.Bank.Api.Tests.Fakes;

public class BankApiFactory : WebApplicationFactory<Program>
{
    public InMemoryEventStore EventStore { get; } = new();
    public InMemoryReadStore ReadStore { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove CosmosDB registrations
            services.RemoveAll<CosmosClient>();
            services.RemoveAll<IEventStore>();
            services.RemoveAll<IReadStore>();

            // Remove Redis registration and replace with fake
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(new FakeConnectionMultiplexer());

            // Register in-memory fakes as singletons (same instance throughout test)
            services.AddSingleton<IEventStore>(EventStore);
            services.AddSingleton<IReadStore>(ReadStore);
        });
    }
}
