using System.Text.Json;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Infrastructure.ReadStore;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;
using Microsoft.Azure.Cosmos;
using StackExchange.Redis;
using Pefi.Bank.Domain.Messages;
using Pefi.Bank.Api.Extensions;

namespace Pefi.Bank.Api.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/customers").WithTags("Customers");

        group.MapPost("/", CreateCustomer).WithName("CreateCustomer");
        group.MapGet("/{id:guid}", GetCustomer).WithName("GetCustomer");
        group.MapPut("/{id:guid}", UpdateCustomer).WithName("UpdateCustomer");
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
        IReadStore readStore,
        IAggregateRepository<Customer> repository)
    {
        var customer = await readStore.GetAsync<CustomerReadModel>(
            id.ToString(), "customer");

        if (customer is not null)
            return Results.Ok(customer);

        // Fall back to event store when projection hasn't run yet
        var aggregate = await repository.LoadAsync(id);
        if (aggregate.Version < 0)
            return Results.NotFound();

        return Results.Ok(new CustomerReadModel
        {
            Id = aggregate.Id,
            FirstName = aggregate.FirstName,
            LastName = aggregate.LastName,
            Email = aggregate.Email,
            AccountCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private static async Task<IResult> UpdateCustomer(
        Guid id,
        UpdateCustomerCommand command,
        IAggregateRepository<Customer> repository)
    {
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
