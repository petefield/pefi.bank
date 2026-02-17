namespace Pefi.Bank.Domain;

public enum EntryType
{
    Debit,
    Credit
}

public sealed record LedgerEntry(
    Guid EntryId,
    Guid AccountId,
    EntryType EntryType,
    decimal Amount,
    string Description);
