using FluentValidation;
using InventoryApp.Application.Dtos;
using InventoryApp.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace InventoryApp.Application.Validators;

/// <summary>
/// Validation for every auth request. The password rules are read from <see cref="IdentityOptions"/>
/// so the same rules apply at registration, at change-password and here, with no chance of drift.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(request => request.Password)
            .NotEmpty();
    }
}

public sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator(IOptions<IdentityOptions> identityOptions)
    {
        var identity = identityOptions.Value;

        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(request => request.FullName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(200);

        RuleFor(request => request.Role)
            .NotEmpty()
            .Must(SystemRoles.All.Contains)
            .WithMessage($"Role must be one of: {string.Join(", ", SystemRoles.All)}.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(identity.Password.RequiredLength)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^A-Za-z0-9]").WithMessage("Password must contain a non-alphanumeric character.");
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty()
            .MaximumLength(512);
    }
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty()
            .MaximumLength(512);
    }
}
