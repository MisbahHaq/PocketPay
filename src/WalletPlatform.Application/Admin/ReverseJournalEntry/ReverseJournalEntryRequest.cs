namespace WalletPlatform.Application.Admin.ReverseJournalEntry;

public sealed record ReverseJournalEntryRequest(string? Description);

public sealed record ReverseJournalEntryResponse(
    Guid ReversalJournalEntryId,
    Guid ReversesJournalEntryId,
    DateTimeOffset CreatedAtUtc);
