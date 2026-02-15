using Pefi.Bank.Api;
using Pefi.Bank.Api.Endpoints;
using Pefi.Bank.Infrastructure;
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

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();

// Initialize CosmosDB containers
await app.Services.InitializeCosmosAsync(databaseName);

// Map endpoints
app.MapCustomerEndpoints();
app.MapAccountEndpoints();
app.MapTransferEndpoints();

app.Run();

// Make the auto-generated Program class accessible for integration tests
public partial class Program;
