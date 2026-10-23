using Microsoft.Extensions.Options;
using WalletPlatform.Application.Common;

namespace WalletPlatform.Infrastructure.Common;

internal sealed class IdempotencyOptionsValidator : IValidateOptions<IdempotencyOptions>
{
    public ValidateOptionsResult Validate(string? name, IdempotencyOptions options)
    {
        var failures = new List<string>();

        if (options.Ttl <= TimeSpan.Zero)
        {
            failures.Add("Idempotency:Ttl must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.HeaderName))
        {
            failures.Add("Idempotency:HeaderName cannot be empty.");
        }

        if (options.RequiredPaths is null || options.RequiredPaths.Length == 0)
        {
            failures.Add("Idempotency:RequiredPaths must contain at least one path.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}