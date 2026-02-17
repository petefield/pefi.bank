using System.Security.Claims;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;
using Microsoft.Azure.Cosmos;
using StackExchange.Redis;
using Pefi.Bank.Api.Extensions;

namespace Pefi.Bank.Api.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/customers").WithTags("Customers");

        group.MapPost("/", CreateCustomer).WithName("CreateCustomer");
        group.MapGet("/{id:guid}", GetCustomer).WithName("GetCustomer").RequireAuthorization();
        group.MapPut("/{id:guid}", UpdateCustomer).WithName("UpdateCustomer").RequireAuthorization();
        group.MapGet("/", ListCustomers).WithName("ListCustomers");
        group.MapGet("/{id:guid}/events", SubscribeToCustomerEvents).WithName("CustomerEvents");
    }

    private static async Task<IResult> CreateCustomer(
        CreateCustomerCommand command,
        IAggregateRepository<Customer> repository)
    {
        var id = Guid.NewGuid();
        var customer = Customer.Create(id, command.FirstName, command.LastName, command.Email);
        await repository.SaveAsync(customer);

        return Results.Accepted($"/customers/{id}", new { id, eventsUrl = $"/customers/{id}/events" });
    }

    private static async Task<IResult> GetCustomer(
        Guid id,
        HttpContext context,
        IReadStore readStore,
        IAggregateRepository<Customer> repository)
    {
        // Verify the authenticated user owns this customer ID
        var claim = context.User.FindFirst("customerId")
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier);
        var authCustomerId = Guid.Parse(claim!.Value);
        if (authCustomerId != id)
            return Results.Forbid();

        var customerReadModel = await readStore.ReadWithFallBack("customer", id, repository, c => new CustomerReadModel
        {
            Id = c.Id,
            FirstName = c.FirstName,
            LastName = c.LastName,
            Email = c.Email,
            AccountCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }); 

        return customerReadModel is not null
            ? Results.Ok(customerReadModel)
            : Results.NotFound();
    }

    private static async Task<IResult> UpdateCustomer(
        Guid id,
        UpdateCustomerCommand command,
        HttpContext context,
        IAggregateRepository<Customer> repository)
    {
        // Verify the authenticated user owns this customer ID
        var claim = context.User.FindFirst("customerId")
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier);
        var authCustomerId = Guid.Parse(claim!.Value);
        if (authCustomerId != id)
            return Results.Forbid();

        var customer = await repository.LoadAsync(id);
        if (customer.Version < 0)
            return Results.NotFound();

        customer.Update(command.FirstName, command.LastName, command.Email);
        await repository.SaveAsync(customer);

        return Results.NoContent();
    }

    private static async Task<IResult> ListCustomers(IReadStore readStore)
    {
        var customers = await readStore.QueryAsync<CustomerReadModel>(
            new QueryDefinition("SELECT * FROM c WHERE c.partitionKey = 'customer'"));

        return Results.Ok(customers);
    }

    private static async Task SubscribeToCustomerEvents(
        Guid id,
        HttpContext context,
        IConnectionMultiplexer redis)
    {
        await redis.SubscribeToEvents(id, "customer-events", context);     
    }
}
