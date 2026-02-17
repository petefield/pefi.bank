namespace Pefi.Bank.Shared.ReadModels;

public sealed class SettlementAccountReadModel
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = "settlement";
    public decimal Balance { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public DateTime UpdatedAt { get; set; }
}
