using FluentValidation;
using WalletPlatform.Application.Common.Security;

namespace WalletPlatform.Application.Auth.RegisterUser;

public sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(request => request.Password)
            .NotEmpty()
            .Must(PasswordPolicy.IsSatisfiedBy)
            .WithMessage(PasswordPolicy.Description);
    }
}
