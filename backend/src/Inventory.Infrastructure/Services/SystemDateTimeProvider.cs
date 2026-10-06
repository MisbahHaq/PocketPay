using Inventory.Application.Abstractions;

namespace Inventory.Infrastructure.Services;

/// <summary>
/// The real clock. Behind <see cref="Abstractions.IDateTimeProvider"/> so that
/// time-dependent behaviour (token expiry, lockouts, reorder windows) can be tested by
/// moving time instead of by sleeping.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    /// <summary>
    /// Always UTC. A stock ledger that records local time is unusable for reconciliation
    /// across warehouses in different time zones, so the database stores an absolute
    /// instant everywhere and formatting happens in the client.
    /// </summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}