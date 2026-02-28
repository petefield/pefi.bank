namespace Pefi.Bank.Shared.ReadModels;

public  record TransferReadModel(
    Guid Id,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Description,
    string Status,
    string? FailureReason,
    DateTime InitiatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt)
{
    public string PartitionKey { get; set; } = "transfer";

}
