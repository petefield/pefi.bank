using Pefi.Bank.Domain.Events;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Aggregates;

public class Account : Aggregate
{
    public Guid CustomerId { get; private set; }
    public string AccountName { get; private set; } = string.Empty;
    public decimal Balance { get; private set; }
    public bool IsClosed { get; private set; }

    public static Account Open(Guid accountId, Guid customerId, string accountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);

        var account = new Account();
        account.RaiseEvent(new AccountOpened(accountId, customerId, accountName));
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

        if (amount <= 0)
            throw new DomainException("Withdrawal amount must be positive.");

        if (amount > Balance)
            throw new DomainException("Insufficient funds.");

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

    protected override void Apply(IEvent @event)
    {
        switch (@event)
        {
            case AccountOpened e:
                Id = e.AccountId;
                CustomerId = e.CustomerId;
                AccountName = e.AccountName;
                Balance = 0;
                IsClosed = false;
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
    }
}
