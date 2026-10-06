using FluentValidation;
using Inventory.Application.Common;

namespace Inventory.Application.Auth.Validators;

/// <summary>
/// Password rules live in exactly two places: here, for an immediate, field-level error
/// the user can act on, and in ASP.NET Identity's options, which is the authority.
/// Both must agree or the API and the UI disagree about what a valid password is.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

public sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256)
            .EmailAddress().WithMessage("Enter a valid email address.");

        RuleFor(request => request.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MinimumLength(2).WithMessage("Full name must be at least 2 characters.")
            .MaximumLength(128);

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(12).WithMessage("Password must be at least 12 characters.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a symbol.")
            .When(request => !string.IsNullOrEmpty(request.Password));

        // Rule-level rather than per-field so the user sees it next to the pair of inputs.
        RuleFor(request => request)
            .Must(request => string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            .WithMessage("Passwords do not match.")
            .OverridePropertyName(nameof(RegisterUserRequest.ConfirmPassword));

        // Defaulting to Staff rather than Admin means a new account can never be
        // over-privileged by omission.
        RuleFor(request => request.Role)
            .IsInEnum()
            .WithMessage("Unknown role.")
            .When(request => !string.IsNullOrWhiteSpace(request.Role));
    }
}