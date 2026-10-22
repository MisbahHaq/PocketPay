using FluentValidation;

namespace WalletPlatform.Application.Ledger.GetWalletStatement;

public sealed class GetWalletStatementQueryValidator : AbstractValidator<GetWalletStatementQuery>
{
    public GetWalletStatementQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
