namespace Pefi.Bank.Shared.Commands;

public sealed record CreateCustomerCommand(
    string FirstName,
    string LastName,
    string Email);

public sealed record UpdateCustomerCommand(
    string FirstName,
    string LastName,
    string Email);
