namespace Pefi.Bank.Shared.ReadModels;

public sealed class LedgerEntryReadModel
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = "ledger";
    public Guid TransactionId { get; set; }
    public Guid AccountId { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
