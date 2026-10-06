using FluentValidation;
using Inventory.Application.Auth;
using Inventory.Application.Auth.Validators;

namespace Inventory.UnitTests.Application;

public sealed class RegisterUserRequestValidatorTests
{
    private static RegisterUserRequestValidator CreateSut() => new();

    private static RegisterUserRequest Valid(
        string email = "warehouse@example.com",
        string password = "Str0ng-Passw0rd!",
        string confirmPassword = "Str0ng-Passw0rd!",
        string fullName = "Alex Okonkwo",
        string? role = null) =>
        new(email, password, confirmPassword, fullName, role);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@example.com")]
    [InlineData("two@@example.com")]
    public void Rejects_an_unusable_email(string email)
    {
        var result = CreateSut().Validate(Valid(email: email));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(RegisterUserRequest.Email));
    }

    [Fact]
    public void Accepts_a_normalised_email()
    {
        CreateSut().Validate(Valid(email: "Warehouse.Lead+stock@Example.co.uk")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Accepts_a_password_meeting_every_complexity_rule()
    {
        CreateSut().Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Sh0rt!")]              // under 12
    [InlineData("nodigitshere!")]       // no digit
    [InlineData("NOLOWERCASE1!")]       // no lowercase
    [InlineData("nolowercase1!")]       // no uppercase
    [InlineData("NoSymbols12345")]      // no symbol
    public void Rejects_a_password_that_misses_a_complexity_rule(string password)
    {
        var result = CreateSut().Validate(Valid(password: password, confirmPassword: password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(RegisterUserRequest.Password));
    }

    [Fact]
    public void Reports_a_password_mismatch_against_the_confirmation_field()
    {
        var result = CreateSut().Validate(Valid(confirmPassword: "Different-Passw0rd!"));

        result.IsValid.Should().BeFalse();

        // Must attach to ConfirmPassword so the UI can highlight the right input.
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(RegisterUserRequest.ConfirmPassword));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Role_is_optional_so_an_admin_only_has_to_choose_when_escalating(string? role)
    {
        CreateSut().Validate(Valid(role: role)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_an_unrecognised_role()
    {
        // "Superuser" must not pass silently and become a role nobody is granted.
        var result = CreateSut().Validate(Valid(role: "Superuser"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(RegisterUserRequest.Role));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Manager")]
    [InlineData("Staff")]
    public void Accepts_every_system_role(string role)
    {
        CreateSut().Validate(Valid(role: role)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("sTaFf")]
    public void Accepts_a_role_regardless_of_casing(string role)
    {
        // The command handler accepts these too, so the validator must not reject them
        // first and turn a working request into a 422.
        CreateSut().Validate(Valid(role: role)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Admin ")]
    [InlineData(" Admin")]
    public void Accepts_a_role_with_surrounding_whitespace(string role)
    {
        CreateSut().Validate(Valid(role: role)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_empty_password_does_not_produce_a_pile_of_complexity_errors()
    {
        // "Required" and "must contain a digit" are not simultaneously useful feedback.
        var result = CreateSut().Validate(Valid(password: "", confirmPassword: ""));

        result.Errors.Count(failure => failure.PropertyName == nameof(RegisterUserRequest.Password))
            .Should().Be(1, "the user needs one clear message, not six");
    }
}

public sealed class LoginRequestValidatorTests
{
    [Fact]
    public void Requires_a_password_even_though_it_is_short()
    {
        // Sign-in must not impose the registration complexity rules; the password was
        // already validated when the account was created, and rejecting it here would
        // lock out users created before a policy change.
        var result = new LoginRequestValidator().Validate(new LoginRequest("user@example.com", "short"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("nope")]
    public void Rejects_a_missing_email(string email)
    {
        new LoginRequestValidator().Validate(new LoginRequest(email, "pw")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_missing_password()
    {
        new LoginRequestValidator().Validate(new LoginRequest("user@example.com", "")).IsValid.Should().BeFalse();
    }
}