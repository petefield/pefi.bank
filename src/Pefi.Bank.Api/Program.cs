using Pefi.Bank.Api;
using Pefi.Bank.Api.Endpoints;
using Pefi.Bank.Auth;
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

app.Run();

// Make the auto-generated Program class accessible for integration tests
public partial class Program;
