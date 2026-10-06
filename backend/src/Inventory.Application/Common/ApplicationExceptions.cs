using FluentValidation.Results;

namespace Inventory.Application.Common;

/// <summary>
/// One structured error, in the shape a form can bind to directly.
/// </summary>
public sealed record Error(string Code, string Message)
{
    /// <summary>Property name this error belongs to, or null for request-level errors.</summary>
    public string? Field { get; init; }
}

/// <summary>
/// Request failed server-side validation.
/// </summary>
/// <remarks>
/// Maps to <c>422 Unprocessable Entity</c>. FluentValidation failures on simple
/// shape problems surface as <c>400</c> via model binding, so 422 is reserved for
/// "the request parsed fine but breaks a rule" - a duplicate SKU, a movement that
/// would drive stock negative, an order in the wrong state. Using one distinct code
/// lets the client tell "fix your fields" apart from "fix your state".
/// </remarks>
public sealed class ApplicationValidationException : Exception
{
    public ApplicationValidationException(IEnumerable<Error> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = [.. errors];
    }

    public ApplicationValidationException(string message, IEnumerable<Error>? errors = null)
        : base(message) => Errors = errors is null ? [] : [.. errors];

    public IReadOnlyList<Error> Errors { get; }

    public static ApplicationValidationException FromFluentResults(IEnumerable<ValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        var errors = failures
            .Select(failure => new Error(
                NormaliseCode(failure.ErrorCode ?? "invalid"),
                failure.ErrorMessage)
            {
                Field = ToCamelCase(failure.PropertyName),
            })
            .ToList();

        return new ApplicationValidationException(errors);
    }

    private static string NormaliseCode(string code) =>
        code.Replace("Validator", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// ASP.NET model binding reports property names in PascalCase; JSON clients expect
    /// camelCase. Converting here keeps the field names usable as-is in a form library.
    /// </summary>
    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0])
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
}

/// <summary>Requested resource does not exist, or the caller may not know that it does. Maps to 404.</summary>
public sealed class NotFoundException(string resourceName, object key)
    : Exception($"{resourceName} '{key}' was not found.")
{
    public string ResourceName { get; } = resourceName;

    public object Key { get; } = key;
}

/// <summary>
/// The request is well-formed but conflicts with current state - a lost concurrency
/// race, a duplicate unique key, an illegal state transition. Maps to 409.
/// </summary>
public sealed class ConflictException(string message, string? code = null) : Exception(message)
{
    /// <summary>Machine-readable discriminator so the client can react without string matching.</summary>
    public string? Code { get; } = code;
}

/// <summary>Credentials are missing, malformed or wrong. Maps to 401.</summary>
public sealed class UnauthorizedException(string message, string? code = null) : Exception(message)
{
    public string? Code { get; } = code;
}

/// <summary>Caller is authenticated but not permitted. Maps to 403.</summary>
public sealed class ForbiddenAccessException(string message, string? code = null) : Exception(message)
{
    public string? Code { get; } = code;
}