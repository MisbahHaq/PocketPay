using FluentValidation;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Admin.FundWallet;

public sealed class FundWalletRequestValidator : AbstractValidator<FundWalletRequest>
{
    public FundWalletRequestValidator()
    {
        RuleFor(request => request.AmountMinorUnits)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(Money.MaxMinorUnits)
            .WithMessage($"Amount must not exceed {Money.MaxMinorUnits} minor units.");
    }
}
