namespace Pefi.Bank.Domain.Messages;

public record EntityStateChangedMessage(string EntityId, string State);