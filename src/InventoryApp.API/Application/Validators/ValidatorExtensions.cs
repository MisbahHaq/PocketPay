using FluentValidation;
using InventoryApp.Application.Common.Exceptions;

namespace InventoryApp.Application.Validators;

/// <summary>
/// Bridges FluentValidation to the application's own exception type.
/// </summary>
/// <remarks>
/// Deliberately not <c>ValidateAndThrowAsync</c>: that throws FluentValidation's own exception,
/// which would force the exception handler to know about the validation library. This way the
/// library's <c>ValidationResult</c> is translated here and the rest of the app only ever sees
/// <see cref="ValidationFailedException"/>.
/// </remarks>
internal static class ValidatorExtensions
{
    public static async Task<T> EnsureValidAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);

        if (result.IsValid)
            return instance;

        throw new ValidationFailedException(result.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal));
    }
}