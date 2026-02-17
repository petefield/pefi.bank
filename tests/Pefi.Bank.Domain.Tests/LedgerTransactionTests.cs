using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Domain.Events;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Tests;

public class LedgerTransactionTests
{
    [Fact]
    public void Record_CreatesBalancedDebitCreditPair()
    {
        var transactionId = Guid.NewGuid();
        var debitAccountId = Guid.NewGuid();
        var creditAccountId = Guid.NewGuid();

        var ledger = LedgerTransaction.Record(
            transactionId, "Deposit", debitAccountId, creditAccountId, 500m, "Initial deposit");

        Assert.Equal(transactionId, ledger.Id);
        Assert.Equal("Deposit", ledger.TransactionType);
        Assert.Equal(debitAccountId, ledger.DebitAccountId);
        Assert.Equal(creditAccountId, ledger.CreditAccountId);
        Assert.Equal(500m, ledger.Amount);
        Assert.Equal("Initial deposit", ledger.Description);

        // Verify balanced entries
        Assert.NotNull(ledger.DebitEntry);
        Assert.NotNull(ledger.CreditEntry);
        Assert.Equal(EntryType.Debit, ledger.DebitEntry.EntryType);
        Assert.Equal(EntryType.Credit, ledger.CreditEntry.EntryType);
        Assert.Equal(500m, ledger.DebitEntry.Amount);
        Assert.Equal(500m, ledger.CreditEntry.Amount);
        Assert.Equal(debitAccountId, ledger.DebitEntry.AccountId);
        Assert.Equal(creditAccountId, ledger.CreditEntry.AccountId);
    }

    [Fact]
    public void Record_ProducesLedgerTransactionRecordedEvent()
    {
        var ledger = LedgerTransaction.Record(
            Guid.NewGuid(), "Deposit", Guid.NewGuid(), Guid.NewGuid(), 100m, "Test deposit");

        Assert.Single(ledger.UncommittedEvents);
        var @event = Assert.IsType<LedgerTransactionRecorded>(ledger.UncommittedEvents[0]);
        Assert.Equal(100m, @event.Amount);
        Assert.Equal("Deposit", @event.TransactionType);
    }

    [Fact]
    public void Record_ZeroAmount_Throws()
    {
        Assert.Throws<DomainException>(() =>
            LedgerTransaction.Record(
                Guid.NewGuid(), "Deposit", Guid.NewGuid(), Guid.NewGuid(), 0m, "Zero"));
    }

    [Fact]
    public void Record_NegativeAmount_Throws()
    {
        Assert.Throws<DomainException>(() =>
            LedgerTransaction.Record(
                Guid.NewGuid(), "Deposit", Guid.NewGuid(), Guid.NewGuid(), -50m, "Negative"));
    }

    [Fact]
    public void Record_SameDebitAndCreditAccount_Throws()
    {
        var accountId = Guid.NewGuid();

        Assert.Throws<DomainException>(() =>
            LedgerTransaction.Record(
                Guid.NewGuid(), "Deposit", accountId, accountId, 100m, "Same account"));
    }

    [Fact]
    public void Record_EmptyTransactionType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            LedgerTransaction.Record(
                Guid.NewGuid(), "", Guid.NewGuid(), Guid.NewGuid(), 100m, "No type"));
    }

    [Fact]
    public void Record_EmptyDescription_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            LedgerTransaction.Record(
                Guid.NewGuid(), "Deposit", Guid.NewGuid(), Guid.NewGuid(), 100m, ""));
    }

    [Fact]
    public void Record_DebitAndCreditEntryIdsAreDifferent()
    {
        var ledger = LedgerTransaction.Record(
            Guid.NewGuid(), "Deposit", Guid.NewGuid(), Guid.NewGuid(), 100m, "Test");

        Assert.NotEqual(ledger.DebitEntry.EntryId, ledger.CreditEntry.EntryId);
    }
}
