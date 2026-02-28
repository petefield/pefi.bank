using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pefi.Bank.Functions.Extensions;
using Pefi.Bank.Functions.Projections;
using Pefi.Bank.Infrastructure;
using StackExchange.Redis;

var builder = FunctionsApplication.CreateBuilder(args);

var cosmosConnection = Environment.GetEnvironmentVariable("CosmosDb__ConnectionString")
    ?? "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
var databaseName = Environment.GetEnvironmentVariable("CosmosDb__DatabaseName") ?? "pefibank";

var redisConnection = Environment.GetEnvironmentVariable("Redis__ConnectionString") ?? "localhost:6379";

var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? "http://localhost:18889";

builder.Services
    .AddCosmosInfrastructure(cosmosConnection, databaseName)
    .AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection))
    .AddSingleton<EventNotificationPublisher>();

builder.Services.AddProjections()
    .AddSagas();

// ── OpenTelemetry ───────────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(DiagnosticConfig.ServiceName, serviceInstanceId: "functions"))
    .WithTracing(tracing => tracing
        .AddSource(DiagnosticConfig.ServiceName)
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddMeter(DiagnosticConfig.ServiceName)
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

builder.Services.Configure<OpenTelemetryLoggerOptions>(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
});

builder.Services.AddOpenTelemetry()
    .WithLogging(logging => logging
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

builder.Build().Run();
