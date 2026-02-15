namespace Pefi.Bank.Shared.Commands;

public sealed record OpenAccountCommand(
    string AccountName);

public sealed record DepositCommand(
    decimal Amount,
    string Description);

public sealed record WithdrawCommand(
    decimal Amount,
    string Description);

public sealed record TransferCommand(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Description);
