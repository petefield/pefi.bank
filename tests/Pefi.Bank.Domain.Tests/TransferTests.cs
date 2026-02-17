using Pefi.Bank.Domain.Aggregates;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Tests;

public class TransferTests
{
    private static Transfer CreateInitiatedTransfer()
    {
        var transfer = Transfer.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "Payment");
        transfer.MarkCommitted();
        return transfer;
    }

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
    public void Initiate_ZeroAmount_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Transfer.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m, "Zero"));
    }

    // ── MarkSourceDebited ───────────────────────────────────────────────────

    [Fact]
    public void MarkSourceDebited_FromInitiated_SetsSourceDebited()
    {
        var transfer = CreateInitiatedTransfer();

        transfer.MarkSourceDebited();

        Assert.Equal(TransferStatus.SourceDebited, transfer.Status);
    }

    [Fact]
    public void MarkSourceDebited_WhenNotInitiated_Throws()
    {
        var transfer = CreateInitiatedTransfer();
        transfer.MarkSourceDebited();
        transfer.MarkCommitted();

        Assert.Throws<DomainException>(() => transfer.MarkSourceDebited());
    }

    // ── MarkDestinationCredited ─────────────────────────────────────────────

    [Fact]
    public void MarkDestinationCredited_FromSourceDebited_SetsDestinationCredited()
    {
        var transfer = CreateInitiatedTransfer();
        transfer.MarkSourceDebited();
        transfer.MarkCommitted();

        transfer.MarkDestinationCredited();

        Assert.Equal(TransferStatus.DestinationCredited, transfer.Status);
    }

    [Fact]
    public void MarkDestinationCredited_WhenNotSourceDebited_Throws()
    {
        var transfer = CreateInitiatedTransfer();

        Assert.Throws<DomainException>(() => transfer.MarkDestinationCredited());
    }

    // ── MarkSourceDebitCompensated ──────────────────────────────────────────

    [Fact]
    public void MarkSourceDebitCompensated_FromSourceDebited_Succeeds()
    {
        var transfer = CreateInitiatedTransfer();
        transfer.MarkSourceDebited();
        transfer.MarkCommitted();

        transfer.MarkSourceDebitCompensated();

        // Status remains SourceDebited — Fail() is called next
        Assert.Equal(TransferStatus.SourceDebited, transfer.Status);
    }

    [Fact]
    public void MarkSourceDebitCompensated_WhenNotSourceDebited_Throws()
    {
        var transfer = CreateInitiatedTransfer();

        Assert.Throws<DomainException>(() => transfer.MarkSourceDebitCompensated());
    }

    // ── Complete ────────────────────────────────────────────────────────────

    [Fact]
    public void Complete_FromDestinationCredited_SetsCompleted()
    {
        var transfer = CreateInitiatedTransfer();
        transfer.MarkSourceDebited();
        transfer.MarkCommitted();
        transfer.MarkDestinationCredited();
        transfer.MarkCommitted();

        transfer.Complete();

        Assert.Equal(TransferStatus.Completed, transfer.Status);
    }

    [Fact]
    public void Complete_WhenNotDestinationCredited_Throws()
    {
        var transfer = CreateInitiatedTransfer();

        Assert.Throws<DomainException>(() => transfer.Complete());
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_Throws()
    {
        var transfer = CreateInitiatedTransfer();
        transfer.MarkSourceDebited();
        transfer.MarkCommitted();
        transfer.MarkDestinationCredited();
        transfer.MarkCommitted();
        transfer.Complete();
        transfer.MarkCommitted();

        Assert.Throws<DomainException>(() => transfer.Complete());
    }

    // ── Fail ────────────────────────────────────────────────────────────────

    [Fact]
    public void Fail_FromInitiated_SetsFailedWithReason()
    {
        var transfer = CreateInitiatedTransfer();

        transfer.Fail("Insufficient funds");

        Assert.Equal(TransferStatus.Failed, transfer.Status);
        Assert.Equal("Insufficient funds", transfer.FailureReason);
    }

    [Fact]
    public void Fail_FromSourceDebited_SetsFailedWithReason()
    {
        var transfer = CreateInitiatedTransfer();
        transfer.MarkSourceDebited();
        transfer.MarkCommitted();

        transfer.Fail("Destination credit failed");

        Assert.Equal(TransferStatus.Failed, transfer.Status);
        Assert.Equal("Destination credit failed", transfer.FailureReason);
    }

    [Fact]
    public void Fail_WhenAlreadyFailed_Throws()
    {
        var transfer = CreateInitiatedTransfer();
        transfer.Fail("First failure");
        transfer.MarkCommitted();

        Assert.Throws<DomainException>(() => transfer.Fail("Second failure"));
    }

    [Fact]
    public void Fail_WhenCompleted_Throws()
    {
        var transfer = CreateInitiatedTransfer();
        transfer.MarkSourceDebited();
        transfer.MarkCommitted();
        transfer.MarkDestinationCredited();
        transfer.MarkCommitted();
        transfer.Complete();
        transfer.MarkCommitted();

        Assert.Throws<DomainException>(() => transfer.Fail("Too late"));
    }

    [Fact]
    public void Fail_FromDestinationCredited_Throws()
    {
        var transfer = CreateInitiatedTransfer();
        transfer.MarkSourceDebited();
        transfer.MarkCommitted();
        transfer.MarkDestinationCredited();
        transfer.MarkCommitted();

        // Once destination is credited, you can only Complete — cannot fail
        Assert.Throws<DomainException>(() => transfer.Fail("Cannot fail now"));
    }

    // ── Full saga happy path ────────────────────────────────────────────────

    [Fact]
    public void FullSagaHappyPath_InitiateToComplete()
    {
        var transferId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var destId = Guid.NewGuid();

        var transfer = Transfer.Initiate(transferId, sourceId, destId, 250m, "Rent");
        Assert.Equal(TransferStatus.Initiated, transfer.Status);
        transfer.MarkCommitted();

        transfer.MarkSourceDebited();
        Assert.Equal(TransferStatus.SourceDebited, transfer.Status);
        transfer.MarkCommitted();

        transfer.MarkDestinationCredited();
        Assert.Equal(TransferStatus.DestinationCredited, transfer.Status);
        transfer.MarkCommitted();

        transfer.Complete();
        Assert.Equal(TransferStatus.Completed, transfer.Status);
        Assert.Null(transfer.FailureReason);
    }

    // ── Full saga compensation path ─────────────────────────────────────────

    [Fact]
    public void FullSagaCompensationPath_DebitThenCompensateAndFail()
    {
        var transfer = Transfer.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "Payment");
        transfer.MarkCommitted();

        transfer.MarkSourceDebited();
        transfer.MarkCommitted();

        // Destination credit fails — compensate and fail
        transfer.MarkSourceDebitCompensated();
        transfer.Fail("Destination account closed");

        Assert.Equal(TransferStatus.Failed, transfer.Status);
        Assert.Equal("Destination account closed", transfer.FailureReason);
    }
}
