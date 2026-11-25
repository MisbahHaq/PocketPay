namespace WalletPlatform.Application.Audit;

/// <summary>
/// Provides the ambient <see cref="AuditContext"/> for the current operation.
/// </summary>
public interface IAuditContextAccessor
{
    AuditContext Current { get; }
}
