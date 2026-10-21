using Microsoft.Extensions.Options;
using WalletPlatform.Application.Common.Security;

namespace WalletPlatform.Infrastructure.Security;

internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private const int MinimumSigningKeyLength = 32;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add("Jwt:SigningKey is required.");
        }
        else if (options.SigningKey.Length < MinimumSigningKeyLength)
        {
            failures.Add($"Jwt:SigningKey must be at least {MinimumSigningKeyLength} characters long.");
        }

        if (options.AccessTokenMinutes <= 0)
        {
            failures.Add("Jwt:AccessTokenMinutes must be greater than zero.");
        }

        if (options.RefreshTokenDays <= 0)
        {
            failures.Add("Jwt:RefreshTokenDays must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
