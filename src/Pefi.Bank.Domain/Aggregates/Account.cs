using Pefi.Bank.Domain.Events;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Aggregates;

public class Account : Aggregate
{
    private const string BankSortCode = "04-00-75";

    public Guid CustomerId { get; private set; }

    public string AccountName { get; private set; } = string.Empty;

    public string AccountNumber { get; private set; } = string.Empty;

    public string SortCode { get; private set; } = string.Empty;

    public decimal Balance { get; private set; }

    public bool IsClosed { get; private set; }

    public DateTime OpenedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public decimal OverdraftLimit { get; private set; } = 0;

    public static Account Open(Guid accountId, Guid customerId, string accountName, decimal overdraftLimit = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);

        var accountNumber = GenerateAccountNumber(accountId);

        var account = new Account();
        account.RaiseEvent(new AccountOpened(accountId, customerId, accountName, accountNumber, BankSortCode, overdraftLimit));
        return account;
    }

    public void Deposit(decimal amount, string description)
    {
        EnsureOpen();

        if (amount <= 0)
            throw new DomainException("Deposit amount must be positive.");

        RaiseEvent(new FundsDeposited(Id, amount, description));
    }

    public void Withdraw(decimal amount, string description)
    {
        EnsureOpen();

        if (OverdraftLimit != -1 && (Balance - amount) < -OverdraftLimit)
            throw new DomainException("Withdrawal would exceed overdraft limit.");

        if (amount <= 0)
            throw new DomainException("Withdrawal amount must be positive.");

        RaiseEvent(new FundsWithdrawn(Id, amount, description));
    }

    public void Close()
    {
        EnsureOpen();

        if (Balance != 0) 
            throw new DomainException("Cannot close an account with a non-zero balance.");

        RaiseEvent(new AccountClosed(Id));
    }

    private void EnsureOpen()
    {
        if (IsClosed)
            throw new DomainException("Account is closed.");
    }

    private static string GenerateAccountNumber(Guid accountId)
    {
        // Derive a deterministic 8-digit account number from the account ID
        var hash = Math.Abs(accountId.GetHashCode());
        return (hash % 90_000_000 + 10_000_000).ToString();
    }

    protected override void Apply(IEvent @event)
    {
        switch (@event)
        {
            case AccountOpened e:
                Id = e.AccountId;
                CustomerId = e.CustomerId;
                AccountName = e.AccountName;
                AccountNumber = e.AccountNumber;
                SortCode = e.SortCode;
                Balance = 0;
                IsClosed = false;
                OpenedAt = e.OccurredAt;
                OverdraftLimit = e.OverdraftLimit;
                break;

            case FundsDeposited e:
                Balance += e.Amount;
                break;

            case FundsWithdrawn e:
                Balance -= e.Amount;

                break;

            case AccountClosed:
                IsClosed = true;
                break;

            default:
                throw new DomainException($"Unknown event type: {@event.GetType().Name}");
        }
        UpdatedAt = @event.OccurredAt;
    }
}
