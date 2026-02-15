namespace Pefi.Bank.Domain.Exceptions;

public class ConcurrencyException(string streamId, int expectedVersion)
    : DomainException($"Concurrency conflict on stream '{streamId}' at expected version {expectedVersion}.")
{
    public string StreamId { get; } = streamId;
    public int ExpectedVersion { get; } = expectedVersion;
}
