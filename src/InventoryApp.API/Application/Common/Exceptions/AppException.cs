namespace InventoryApp.Application.Common.Exceptions;

/// <summary>
/// Base type for expected failures. The global exception handler maps each subtype to a status
/// code, which keeps controllers and services free of HTTP concerns.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message)
    {
    }

    /// <summary>HTTP status code the exception maps to. Overridden per subtype.</summary>
    public abstract int StatusCode { get; }

    /// <summary>Short, non-sensitive title included in the ProblemDetails payload.</summary>
    public abstract string Title { get; }
}
