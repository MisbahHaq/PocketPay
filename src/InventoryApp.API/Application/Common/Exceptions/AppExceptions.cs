using System.Net;

namespace InventoryApp.Application.Common.Exceptions;

/// <summary>
/// Request payload failed validation. Named "ValidationFailed" rather than "Validation" to avoid
/// colliding with FluentValidation.ValidationException and FluentResults.ValidationException.
/// </summary>
public sealed class ValidationFailedException : AppException
{
    public ValidationFailedException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IDictionary<string, string[]> Errors { get; }

    public override int StatusCode => (int)HttpStatusCode.BadRequest;

    public override string Title => "Validation failed";
}

/// <summary>
/// Authentication failed. Deliberately says nothing about which credential was wrong, so a
/// caller cannot use the endpoint to discover which email addresses have accounts.
/// </summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message)
    {
    }

    public override int StatusCode => (int)HttpStatusCode.Unauthorized;

    public override string Title => "Unauthorized";

    public static UnauthorizedException InvalidCredentials() =>
        new("Invalid email or password.");

    public static UnauthorizedException InvalidRefreshToken() =>
        new("The refresh token is invalid or has expired.");

    public static UnauthorizedException AccountLockedOut() =>
        new("This account is temporarily locked. Try again later or contact an administrator.");
}

/// <summary>Caller is authenticated but not permitted to perform the operation.</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message)
    {
    }

    public override int StatusCode => (int)HttpStatusCode.Forbidden;

    public override string Title => "Forbidden";
}

/// <summary>Requested resource does not exist.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public override int StatusCode => (int)HttpStatusCode.NotFound;

    public override string Title => "Resource not found";
}

/// <summary>The request conflicts with current state, e.g. an email that is already registered.</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message)
    {
    }

    public override int StatusCode => (int)HttpStatusCode.Conflict;

    public override string Title => "Conflict";
}
