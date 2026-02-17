using Pefi.Bank.Domain.Events;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Aggregates;

public class SettlementAccount : Aggregate
{
    public static readonly Guid WellKnownId = new("00000000-0000-0000-0000-000000000001");

    public decimal Balance { get; private set; }

    public static SettlementAccount Create()
    {
        var account = new SettlementAccount();
        account.RaiseEvent(new SettlementAccountCreated(WellKnownId));
        return account;
    }

    public void Debit(decimal amount, string description)
    {
        if (amount <= 0)
            throw new DomainException("Debit amount must be positive.");

        RaiseEvent(new SettlementDebited(Id, amount, description));
    }

    public void Credit(decimal amount, string description)
    {
        if (amount <= 0)
            throw new DomainException("Credit amount must be positive.");

        RaiseEvent(new SettlementCredited(Id, amount, description));
    }

    protected override void Apply(IEvent @event)
    {
        switch (@event)
        {
            case SettlementAccountCreated e:
                Id = e.AccountId;
                Balance = 0;
                break;

            case SettlementDebited e:
                Balance -= e.Amount;
                break;

            case SettlementCredited e:
                Balance += e.Amount;
                break;

            default:
                throw new DomainException($"Unknown event type: {@event.GetType().Name}");
        }
    }
}
