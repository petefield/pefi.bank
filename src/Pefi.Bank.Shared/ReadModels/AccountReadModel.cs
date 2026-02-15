namespace Pefi.Bank.Shared.ReadModels;

public sealed class AccountReadModel
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = "account";
    public Guid CustomerId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public bool IsClosed { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
