using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Security;

namespace WalletPlatform.Application.Admin.ReverseJournalEntry;

/// <summary>
/// Reverses a journal entry by posting its mirror image. The original is never edited;
/// the reversal is a new entry linked to it. An entry can be reversed at most once.
/// </summary>
public sealed class ReverseJournalEntryHandler(ILedgerService ledger, IClock clock)
{
    public async Task<ReverseJournalEntryResponse> HandleAsync(
        Guid journalEntryId,
        ReverseJournalEntryRequest request,
        string reversedBy,
        CancellationToken cancellationToken)
    {
        var reversal = await ledger.ReverseAsync(
            journalEntryId,
            reversedBy,
            clock.UtcNow,
            request.Description,
            cancellationToken);

        return new ReverseJournalEntryResponse(
            reversal.Id,
            journalEntryId,
            reversal.CreatedAtUtc);
    }
}
