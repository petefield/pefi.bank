namespace Pefi.Bank.Shared.ReadModels;

 public   enum TransactionType { Credit, Debit }


public sealed class TransactionReadModel
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = "transaction";
    public Guid AccountId { get; set; }
    public TransactionType Type { get; set; } 
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal BalanceAfter { get; set; }
    public DateTime OccurredAt { get; set; }
}
