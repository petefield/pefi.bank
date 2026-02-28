namespace Pefi.Bank.Shared.ReadModels;



public record AccountReadModel(
    Guid Id,
    Guid CustomerId,
    string AccountName,
    string AccountNumber,
    string SortCode,
    decimal Balance,
    bool IsClosed,
    DateTime OpenedAt,
    DateTime UpdatedAt,
    decimal OverdraftLimit)
{
    public string PartitionKey { get;  } = "account";

    public decimal Balance { get; set; } = Balance;
}
