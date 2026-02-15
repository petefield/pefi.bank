namespace Pefi.Bank.Shared.ReadModels;

public sealed class TransactionReadModel
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = "transaction";
    public Guid AccountId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal BalanceAfter { get; set; }
    public DateTime OccurredAt { get; set; }
}
