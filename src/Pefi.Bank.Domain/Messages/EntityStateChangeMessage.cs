namespace Pefi.Bank.Domain.Messages;

public class EntityStateChangedMessage
{
    public string EntityId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}