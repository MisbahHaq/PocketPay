namespace Inventory.Domain.Common;

/// <summary>
/// Signals a broken business invariant. A 400-class client mistake, not a bug.
/// </summary>
/// <remarks>
/// Kept free of HTTP concepts so the Domain layer never learns what a status code
/// is. The API's exception middleware is what decides this becomes a 422.
/// </remarks>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, params string[] violations)
        : base(message)
    {
        Violations = violations;
    }

    /// <summary>Per-field messages, when the exception is about specific inputs.</summary>
    public IReadOnlyList<string> Violations { get; } = [];
}

/// <summary>
/// Thrown when an invariant is broken by a caller inside the application boundary
/// (as opposed to <see cref="DomainException"/>, which is about domain rules).
/// Distinct from a plain <see cref="UnauthorizedAccessException"/> so the middleware
/// can map it to 403 rather than 500.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException(string message, string code)
        : base(message) => Code = code;

    /// <summary>Machine-readable reason, surfaced in the ProblemDetails payload.</summary>
    public string Code { get; } = "forbidden";
}