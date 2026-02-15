using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Tests;

public class CustomerTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var customer = Customer.Create(id, "John", "Doe", "john@example.com");

        Assert.Equal(id, customer.Id);
        Assert.Equal("John", customer.FirstName);
        Assert.Equal("Doe", customer.LastName);
        Assert.Equal("john@example.com", customer.Email);
        Assert.Single(customer.UncommittedEvents);
    }

    [Fact]
    public void Create_WithEmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Customer.Create(Guid.NewGuid(), "", "Doe", "john@example.com"));
    }

    [Fact]
    public void Update_ChangesProperties()
    {
        var customer = Customer.Create(Guid.NewGuid(), "John", "Doe", "john@example.com");
        customer.MarkCommitted();

        customer.Update("Jane", "Smith", "jane@example.com");

        Assert.Equal("Jane", customer.FirstName);
        Assert.Equal("Smith", customer.LastName);
        Assert.Equal("jane@example.com", customer.Email);
        Assert.Single(customer.UncommittedEvents);
    }
}
