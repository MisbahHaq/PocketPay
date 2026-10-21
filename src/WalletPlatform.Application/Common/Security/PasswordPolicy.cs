namespace WalletPlatform.Application.Common.Security;

public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public const string Description =
        "Password must be at least 8 characters long and contain an uppercase letter, " +
        "a lowercase letter, a digit and a non-alphanumeric character.";

    public static bool IsSatisfiedBy(string? password) =>
        !string.IsNullOrWhiteSpace(password)
        && password.Length >= MinimumLength
        && password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsAsciiDigit)
        && password.Any(character => !char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character));
}
