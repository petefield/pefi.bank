using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Tests;

public class AccountTests
{
    [Fact]
    public void Open_CreatesAccountWithZeroBalance()
    {
        var account = Account.Open(Guid.NewGuid(), Guid.NewGuid(), "Checking");

        Assert.Equal(0, account.Balance);
        Assert.False(account.IsClosed);
        Assert.Equal("Checking", account.AccountName);
        Assert.Equal(8, account.AccountNumber.Length);
        Assert.Equal("04-00-75", account.SortCode);
        Assert.Single(account.UncommittedEvents);
    }

    [Fact]
    public void Deposit_IncreasesBalance()
    {
        var account = Account.Open(Guid.NewGuid(), Guid.NewGuid(), "Checking");
        account.MarkCommitted();

        account.Deposit(100m, "Initial deposit");

        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void Withdraw_DecreasesBalance()
    {
        var account = Account.Open(Guid.NewGuid(), Guid.NewGuid(), "Checking");
        account.Deposit(200m, "Deposit");
        account.MarkCommitted();

        account.Withdraw(50m, "ATM withdrawal");

        Assert.Equal(150m, account.Balance);
    }

    [Fact]
    public void Withdraw_InsufficientFunds_Throws()
    {
        var account = Account.Open(Guid.NewGuid(), Guid.NewGuid(), "Checking");
        account.Deposit(50m, "Deposit");
        account.MarkCommitted();

        var ex = Assert.Throws<DomainException>(() =>
            account.Withdraw(100m, "Too much"));

        Assert.Equal("Withdrawal would exceed overdraft limit.", ex.Message);
    }

    [Fact]
    public void Withdraw_NegativeAmount_Throws()
    {
        var account = Account.Open(Guid.NewGuid(), Guid.NewGuid(), "Checking");
        account.Deposit(100m, "Deposit");
        account.MarkCommitted();

        Assert.Throws<DomainException>(() =>
            account.Withdraw(-10m, "Negative"));
    }

    [Fact]
    public void Close_WithZeroBalance_Succeeds()
    {
        var account = Account.Open(Guid.NewGuid(), Guid.NewGuid(), "Checking");
        account.MarkCommitted();

        account.Close();

        Assert.True(account.IsClosed);
    }

    [Fact]
    public void Close_WithNonZeroBalance_Throws()
    {
        var account = Account.Open(Guid.NewGuid(), Guid.NewGuid(), "Checking");
        account.Deposit(100m, "Deposit");
        account.MarkCommitted();

        Assert.Throws<DomainException>(() => account.Close());
    }

    [Fact]
    public void Operations_OnClosedAccount_Throw()
    {
        var account = Account.Open(Guid.NewGuid(), Guid.NewGuid(), "Checking");
        account.Close();
        account.MarkCommitted();

        Assert.Throws<DomainException>(() => account.Deposit(100m, "Deposit"));
        Assert.Throws<DomainException>(() => account.Withdraw(100m, "Withdraw"));
    }
}
