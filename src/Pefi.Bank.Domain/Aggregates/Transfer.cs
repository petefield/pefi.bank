using Pefi.Bank.Domain.Events;
using Pefi.Bank.Domain.Exceptions;

namespace Pefi.Bank.Domain.Aggregates;

public enum TransferStatus
{
    Initiated,
    SourceDebited,
    DestinationCredited,
    Completed,
    Failed
}

public class Transfer : Aggregate
{
    public Guid SourceAccountId { get; private set; }
    public Guid DestinationAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public TransferStatus Status { get; private set; }
    public string? FailureReason { get; private set; }

    public static Transfer Initiate(
        Guid transferId,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string description)
    {
        if (amount <= 0)
            throw new DomainException("Transfer amount must be positive.");

        if (sourceAccountId == destinationAccountId)
            throw new DomainException("Cannot transfer to the same account.");

        var transfer = new Transfer();
        transfer.RaiseEvent(new TransferInitiated(transferId, sourceAccountId, destinationAccountId, amount, description));
        return transfer;
    }

    public void MarkSourceDebited()
    {
        if (Status != TransferStatus.Initiated)
            throw new DomainException("Transfer must be in Initiated state to mark source debited.");

        RaiseEvent(new TransferSourceDebited(Id));
    }

    public void MarkDestinationCredited()
    {
        if (Status != TransferStatus.SourceDebited)
            throw new DomainException("Transfer must be in SourceDebited state to mark destination credited.");

        RaiseEvent(new TransferDestinationCredited(Id));
    }

    public void MarkSourceDebitCompensated()
    {
        if (Status != TransferStatus.SourceDebited)
            throw new DomainException("Transfer must be in SourceDebited state to compensate source debit.");

        RaiseEvent(new TransferSourceDebitCompensated(Id));
    }

    public void Complete()
    {
        if (Status != TransferStatus.DestinationCredited)
            throw new DomainException("Transfer must be in DestinationCredited state to complete.");

        RaiseEvent(new TransferCompleted(Id));
    }

    public void Fail(string reason)
    {
        if (Status is not (TransferStatus.Initiated or TransferStatus.SourceDebited))
            throw new DomainException("Transfer cannot be failed from its current state.");

        RaiseEvent(new TransferFailed(Id, reason));
    }

    protected override void Apply(IEvent @event)
    {
        switch (@event)
        {
            case TransferInitiated e:
                Id = e.TransferId;
                SourceAccountId = e.SourceAccountId;
                DestinationAccountId = e.DestinationAccountId;
                Amount = e.Amount;
                Description = e.Description;
                Status = TransferStatus.Initiated;
                break;

            case TransferSourceDebited:
                Status = TransferStatus.SourceDebited;
                break;

            case TransferDestinationCredited:
                Status = TransferStatus.DestinationCredited;
                break;

            case TransferSourceDebitCompensated:
                // Status stays at SourceDebited — Fail() will be called next
                break;

            case TransferCompleted:
                Status = TransferStatus.Completed;
                break;

            case TransferFailed e:
                Status = TransferStatus.Failed;
                FailureReason = e.Reason;
                break;

            default:
                throw new DomainException($"Unknown event type: {@event.GetType().Name}");
        }
    }
}
