using FluentValidation;

namespace WalletPlatform.Application.Admin.ReverseJournalEntry;

public sealed class ReverseJournalEntryRequestValidator : AbstractValidator<ReverseJournalEntryRequest>
{
    public ReverseJournalEntryRequestValidator()
    {
        RuleFor(request => request.Description)
            .MaximumLength(500);
    }
}
