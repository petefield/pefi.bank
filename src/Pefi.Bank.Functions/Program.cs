using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Pefi.Bank.Functions.Extensions;
using Pefi.Bank.Infrastructure;
using StackExchange.Redis;
using Pefi.Bank.Functions.Projections;

var builder = FunctionsApplication.CreateBuilder(args);

var cosmosConnection = Environment.GetEnvironmentVariable("CosmosDb__ConnectionString")
    ?? "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
var databaseName = Environment.GetEnvironmentVariable("CosmosDb__DatabaseName") ?? "pefibank";


var redisConnection = Environment.GetEnvironmentVariable("Redis__ConnectionString") ?? "localhost:6379";

builder.Services
    .AddCosmosInfrastructure(cosmosConnection, databaseName)
    .AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection))
    .AddSingleton<EventNotificationPublisher>();

builder.Services.AddProjections()
    .AddSagas();
    
builder.Build().Run();
