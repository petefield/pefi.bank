using System.Security.Claims;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.Queries;
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
        ICustomerQueries customerQueries)
    {
        // Verify the authenticated user owns this customer ID
        var claim = context.User.FindFirst("customerId")
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier);
        var authCustomerId = Guid.Parse(claim!.Value);
        if (authCustomerId != id)
            return Results.Forbid();

        var customer = await customerQueries.GetByIdAsync(id);
        return customer is not null
            ? Results.Ok(customer)
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

    private static async Task<IResult> ListCustomers(ICustomerQueries customerQueries)
    {
        var customers = await customerQueries.ListAllAsync();
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
