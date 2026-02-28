namespace Pefi.Bank.Shared.ReadModels;

public record class CustomerReadModel
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = "customer";
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int AccountCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
