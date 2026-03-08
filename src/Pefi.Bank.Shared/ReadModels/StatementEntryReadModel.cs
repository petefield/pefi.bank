namespace Pefi.Bank.Shared.ReadModels;

public enum StatementEntryType { Credit, Debit }

public sealed class StatementEntryReadModel
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = "statement-entry";
    public Guid AccountId { get; set; }
    public StatementEntryType Type { get; set; } 
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal BalanceAfter { get; set; }
    public DateTime OccurredAt { get; set; }
}
