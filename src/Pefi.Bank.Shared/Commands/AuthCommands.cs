namespace Pefi.Bank.Shared.Commands;

public sealed record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password);

public sealed record LoginCommand(
    string Email,
    string Password);
