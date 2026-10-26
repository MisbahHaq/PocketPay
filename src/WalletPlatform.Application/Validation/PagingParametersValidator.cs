using FluentValidation;

namespace WalletPlatform.Application.Validation;

public sealed class PagingParametersValidator : AbstractValidator<(int Page, int PageSize)>
{
    public PagingParametersValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
