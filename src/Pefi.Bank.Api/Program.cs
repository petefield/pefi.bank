using HealthChecks.CosmosDb;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pefi.Bank.Api;
using Pefi.Bank.Api.Endpoints;
using Pefi.Bank.Auth;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Infrastructure;
using Pefi.Bank.Shared;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var cosmosConnection = builder.Configuration.GetConnectionString("CosmosDb")
    ?? "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
var databaseName = builder.Configuration.GetValue<string>("CosmosDb:DatabaseName") ?? "pefibank";

builder.Services.AddCosmosInfrastructure(cosmosConnection, databaseName);

var redisConnection = builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(redisConnection));

// JWT Authentication
var jwtSettings = new JwtSettings
{
    Secret = builder.Configuration.GetValue<string>("Jwt:Secret")
        ?? "PefiBankDevelopmentSecretKey_MustBeAtLeast32BytesLong!",
    Issuer = builder.Configuration.GetValue<string>("Jwt:Issuer") ?? "PefiBank",
    Audience = builder.Configuration.GetValue<string>("Jwt:Audience") ?? "PefiBankCustomers",
    ExpiryMinutes = builder.Configuration.GetValue<int?>("Jwt:ExpiryMinutes") ?? 480
};

builder.Services.AddPefiAuthentication(jwtSettings, cosmosConnection, databaseName);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// ── OpenTelemetry ───────────────────────────────────────────────────────────
var otlpEndpoint = builder.Configuration.GetValue<string>("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? "http://localhost:18889";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(DiagnosticConfig.ServiceName, serviceInstanceId: "api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(DiagnosticConfig.ServiceName)
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(DiagnosticConfig.ServiceName)
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
    logging.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
});

// ── Health Checks ───────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddAzureCosmosDB(
        clientFactory: sp => sp.GetRequiredService<CosmosClientHolder>().Client,
        optionsFactory: _ => new() { DatabaseId = databaseName },
        name: "cosmosdb",
        tags: ["ready"])
    .AddRedis(redisConnection, name: "redis", tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Initialize CosmosDB containers
await app.Services.InitializeCosmosAsync(databaseName);

// Map endpoints
app.MapAuthEndpoints();
app.MapCustomerEndpoints();
app.MapAccountEndpoints();
app.MapTransferEndpoints();
app.MapLedgerEndpoints();
app.MapHealthChecks("/health");

// Ensure settlement account exists
using (var scope = app.Services.CreateScope())
{
    var settlementRepo = scope.ServiceProvider.GetRequiredService<IAggregateRepository<Account>>();
    var existingSettlement = await settlementRepo.LoadAsync(WellKnownAccounts.SettlementAccountId);
    if (existingSettlement.Version < 0)
    {
        var settlementAccount = Account.Open(WellKnownAccounts.SettlementAccountId, Guid.Empty, "Settlement Account",-1);
        await settlementRepo.SaveAsync(settlementAccount);
    }
}


app.Run();

// Make the auto-generated Program class accessible for integration tests
public partial class Program;
