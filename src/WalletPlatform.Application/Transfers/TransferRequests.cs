using FluentValidation;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Domain.Entities;

namespace WalletPlatform.Application.Transfers;

public sealed record CreateTransferRequest(
    Guid SourceWalletId,
    string DestinationWalletNumber,
    long AmountMinorUnits,
    Currency Currency,
    string? Note);

public sealed class CreateTransferRequestValidator : AbstractValidator<CreateTransferRequest>
{
    public CreateTransferRequestValidator()
    {
        RuleFor(x => x.SourceWalletId)
            .NotEmpty();

        RuleFor(x => x.DestinationWalletNumber)
            .NotEmpty()
            .Length(12)
            .Matches("^[0-9]{12}$")
            .WithMessage("Wallet number must be exactly 12 digits.");

        RuleFor(x => x.AmountMinorUnits)
            .GreaterThan(0);

        RuleFor(x => x.Currency)
            .IsInEnum();

        RuleFor(x => x.Note)
            .MaximumLength(Transfer.MaxNoteLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Note));
    }
}

public sealed record GetTransfersQuery(
    Guid? WalletId,
    string? Direction, // "outgoing" | "incoming"
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20);

public sealed class GetTransfersQueryValidator : AbstractValidator<GetTransfersQuery>
{
    public GetTransfersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.Direction)
            .Must(d => d is null or "outgoing" or "incoming")
            .WithMessage("Direction must be 'outgoing' or 'incoming'.")
            .When(x => !string.IsNullOrWhiteSpace(x.Direction));
    }
}