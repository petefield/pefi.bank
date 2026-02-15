using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Tests;

public class TransferTests
{
    [Fact]
    public void Initiate_WithValidData_CreatesTransfer()
    {
        var sourceId = Guid.NewGuid();
        var destId = Guid.NewGuid();

        var transfer = Transfer.Initiate(Guid.NewGuid(), sourceId, destId, 100m, "Payment");

        Assert.Equal(TransferStatus.Initiated, transfer.Status);
        Assert.Equal(100m, transfer.Amount);
        Assert.Single(transfer.UncommittedEvents);
    }

    [Fact]
    public void Initiate_SameAccount_Throws()
    {
        var accountId = Guid.NewGuid();

        Assert.Throws<DomainException>(() =>
            Transfer.Initiate(Guid.NewGuid(), accountId, accountId, 100m, "Self transfer"));
    }

    [Fact]
    public void Initiate_NegativeAmount_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Transfer.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -50m, "Negative"));
    }

    [Fact]
    public void Complete_SetsStatusToCompleted()
    {
        var transfer = Transfer.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "Payment");
        transfer.MarkCommitted();

        transfer.Complete();

        Assert.Equal(TransferStatus.Completed, transfer.Status);
    }

    [Fact]
    public void Fail_SetsStatusAndReason()
    {
        var transfer = Transfer.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "Payment");
        transfer.MarkCommitted();

        transfer.Fail("Insufficient funds");

        Assert.Equal(TransferStatus.Failed, transfer.Status);
        Assert.Equal("Insufficient funds", transfer.FailureReason);
    }

    [Fact]
    public void Complete_WhenNotInitiated_Throws()
    {
        var transfer = Transfer.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "Payment");
        transfer.Complete();
        transfer.MarkCommitted();

        Assert.Throws<DomainException>(() => transfer.Complete());
    }
}
