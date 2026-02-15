namespace Pefi.Bank.Shared.ReadModels;

public sealed class TransferReadModel
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = "transfer";
    public Guid SourceAccountId { get; set; }
    public Guid DestinationAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
