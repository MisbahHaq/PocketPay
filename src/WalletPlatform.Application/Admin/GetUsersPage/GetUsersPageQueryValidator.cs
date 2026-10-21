using FluentValidation;

namespace WalletPlatform.Application.Admin.GetUsersPage;

public sealed class GetUsersPageQueryValidator : AbstractValidator<GetUsersPageQuery>
{
    public GetUsersPageQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
