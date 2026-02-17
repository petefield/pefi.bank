using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Domain.Events;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Tests;

public class SettlementAccountTests
{
    [Fact]
    public void Create_SetsWellKnownIdAndZeroBalance()
    {
        var settlement = SettlementAccount.Create();

        Assert.Equal(SettlementAccount.WellKnownId, settlement.Id);
        Assert.Equal(0m, settlement.Balance);
        Assert.Single(settlement.UncommittedEvents);
        Assert.IsType<SettlementAccountCreated>(settlement.UncommittedEvents[0]);
    }

    [Fact]
    public void Debit_DecreasesBalance()
    {
        var settlement = SettlementAccount.Create();
        settlement.MarkCommitted();

        settlement.Debit(500m, "Customer deposit");

        Assert.Equal(-500m, settlement.Balance);
        Assert.Single(settlement.UncommittedEvents);
    }

    [Fact]
    public void Credit_IncreasesBalance()
    {
        var settlement = SettlementAccount.Create();
        settlement.MarkCommitted();

        settlement.Credit(200m, "Customer withdrawal");

        Assert.Equal(200m, settlement.Balance);
        Assert.Single(settlement.UncommittedEvents);
    }

    [Fact]
    public void Debit_ZeroAmount_Throws()
    {
        var settlement = SettlementAccount.Create();
        settlement.MarkCommitted();

        Assert.Throws<DomainException>(() =>
            settlement.Debit(0m, "Zero debit"));
    }

    [Fact]
    public void Credit_NegativeAmount_Throws()
    {
        var settlement = SettlementAccount.Create();
        settlement.MarkCommitted();

        Assert.Throws<DomainException>(() =>
            settlement.Credit(-10m, "Negative credit"));
    }

    [Fact]
    public void MultipleOperations_TrackBalanceCorrectly()
    {
        var settlement = SettlementAccount.Create();
        settlement.MarkCommitted();

        settlement.Debit(1000m, "Deposit 1");
        settlement.Credit(300m, "Withdrawal 1");
        settlement.Debit(500m, "Deposit 2");

        // Balance: 0 - 1000 + 300 - 500 = -1200
        Assert.Equal(-1200m, settlement.Balance);
        Assert.Equal(3, settlement.UncommittedEvents.Count);
    }
}
