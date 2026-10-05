using System.Diagnostics.CodeAnalysis;

namespace InventoryApp.Application.Common.Exceptions;

/// <summary>
/// Base for expected, client-correctable failures. The API middleware maps each
/// subclass to a specific status code, so services can just throw and stay
/// ignorant of HTTP.
/// </summary>
public abstract class ApplicationExceptionBase : Exception
{
    protected ApplicationExceptionBase(string message)
        : base(message)
    {
    }

    protected ApplicationExceptionBase(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// 400 - the request was well formed but conflicts with current state.
/// </summary>
public sealed class ValidationFailedException : ApplicationExceptionBase
{
    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>
/// 401 - caller is not authenticated. Deliberately says nothing about *why*:
/// the same exception is thrown for an unknown email and a wrong password so the
/// endpoint cannot be used to enumerate accounts.
/// </summary>
public sealed class InvalidCredentialsException : ApplicationExceptionBase
{
    public InvalidCredentialsException()
        : base("Invalid email or password.")
    {
    }

    public InvalidCredentialsException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// 401 - authenticated, but the presented refresh token is not usable.
/// </summary>
public sealed class InvalidRefreshTokenException : ApplicationExceptionBase
{
    public InvalidRefreshTokenException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// 403 - authenticated but not allowed.
/// </summary>
public sealed class ForbiddenOperationException : ApplicationExceptionBase
{
    public ForbiddenOperationException(string message)
        : base(message)
    {
    }
}

/// <summary>404 - referenced entity does not exist.</summary>
public sealed class EntityNotFoundException : ApplicationExceptionBase
{
    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.")
    {
    }
}

/// <summary>409 - the request conflicts with existing data.</summary>
public sealed class ConflictException : ApplicationExceptionBase
{
    public ConflictException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Signals that a startup precondition was not met (missing or malformed
/// configuration). Thrown while composing the host so a misconfigured
/// deployment fails immediately instead of at the first login.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ConfigurationException : ApplicationExceptionBase
{
    public ConfigurationException(string message)
        : base(message)
    {
    }
}
