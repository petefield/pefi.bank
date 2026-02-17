using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pefi.Bank.Auth;
using Pefi.Bank.Domain;
using Pefi.Bank.Infrastructure.ReadStore;
using StackExchange.Redis;

namespace Pefi.Bank.Api.Tests.Fakes;

public class BankApiFactory : WebApplicationFactory<Program>
{
    public InMemoryEventStore EventStore { get; } = new();
    public InMemoryReadStore ReadStore { get; } = new();

    private static readonly JwtSettings TestJwtSettings = new()
    {
        // Must match the default fallback in Program.cs
        Secret = "PefiBankDevelopmentSecretKey_MustBeAtLeast32BytesLong!",
        Issuer = "PefiBank",
        Audience = "PefiBankCustomers",
        ExpiryMinutes = 60
    };

    private static readonly JwtTokenService TestTokenService = new(TestJwtSettings);

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

            // Replace Cosmos-based user store with in-memory
            services.RemoveAll<IUserStore<ApplicationUser>>();
            services.AddSingleton<IUserStore<ApplicationUser>>(new InMemoryUserStore());
        });
    }

    /// <summary>
    /// Creates an HttpClient with a valid JWT Bearer token for the specified customer ID.
    /// Also registers the user in the auth system so endpoints can find them.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(Guid customerId, string email = "test@example.com")
    {
        var user = new ApplicationUser
        {
            Id = customerId.ToString(),
            Email = email,
            UserName = email
        };

        var token = TestTokenService.GenerateToken(user);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
