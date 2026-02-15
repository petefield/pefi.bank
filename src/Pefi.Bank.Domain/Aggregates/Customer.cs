using Pefi.Bank.Domain.Events;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Aggregates;

public class Customer : Aggregate
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    public static Customer Create(Guid id, string firstName, string lastName, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var customer = new Customer();
        customer.RaiseEvent(new CustomerCreated(id, firstName, lastName, email));
        return customer;
    }

    public void Update(string firstName, string lastName, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        RaiseEvent(new CustomerUpdated(Id, firstName, lastName, email));
    }

    protected override void Apply(IEvent @event)
    {
        switch (@event)
        {
            case CustomerCreated e:
                Id = e.CustomerId;
                FirstName = e.FirstName;
                LastName = e.LastName;
                Email = e.Email;
                break;

            case CustomerUpdated e:
                FirstName = e.FirstName;
                LastName = e.LastName;
                Email = e.Email;
                break;

            default:
                throw new DomainException($"Unknown event type: {@event.GetType().Name}");
        }
    }
}
