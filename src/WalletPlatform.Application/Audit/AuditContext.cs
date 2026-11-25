namespace WalletPlatform.Application.Audit;

/// <summary>
/// Ambient information about the actor performing an audited operation. The
/// infrastructure layer captures this from the current HTTP request so application
/// handlers do not need to know about ASP.NET Core.
/// </summary>
public sealed record AuditContext(
    Guid? ActorUserId,
    string? ActorRole,
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent)
{
    public static AuditContext Empty { get; } = new(null, null, null, null, null);
}
