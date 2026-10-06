namespace Inventory.Application.Abstractions;

/// <summary>
/// Abstraction over the system clock.
/// </summary>
/// <remarks>
/// Injected rather than calling <c>DateTimeOffset.UtcNow</c> directly for one reason:
/// token expiry, lockout windows and business-day cut-offs are all time-dependent
/// behaviour that must be testable. A test for "this refresh token has expired" should
/// be able to move time, not sleep for a week.
/// </remarks>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}