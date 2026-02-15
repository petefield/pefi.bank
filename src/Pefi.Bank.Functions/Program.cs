using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Pefi.Bank.Infrastructure;
using StackExchange.Redis;

var builder = FunctionsApplication.CreateBuilder(args);

var cosmosConnection = Environment.GetEnvironmentVariable("CosmosDb__ConnectionString")
    ?? "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
var databaseName = Environment.GetEnvironmentVariable("CosmosDb__DatabaseName") ?? "pefibank";

builder.Services.AddCosmosInfrastructure(cosmosConnection, databaseName);

var redisConnection = Environment.GetEnvironmentVariable("Redis__ConnectionString") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnection));

builder.Build().Run();
