namespace InventoryApp.Tests.Support;

/// <summary>
/// Hand-rolled TimeProvider so token expiry can be driven from a test without an extra package.
/// TimeProvider.System has one virtual member, so overriding it is enough.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}