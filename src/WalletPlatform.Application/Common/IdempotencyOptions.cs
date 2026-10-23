namespace WalletPlatform.Application.Common;

/// <summary>
/// Configuration for idempotency behavior.
/// </summary>
public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    /// <summary>
    /// Time-to-live for idempotency records. Default: 24 hours.
    /// Records older than this are cleaned up by the background job.
    /// </summary>
    public TimeSpan Ttl { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Header name for the idempotency key. Default: "Idempotency-Key".
    /// </summary>
    public string HeaderName { get; init; } = "Idempotency-Key";

    /// <summary>
    /// Paths that require idempotency keys. Default: transfer and fund endpoints.
    /// </summary>
    public string[] RequiredPaths { get; init; } =
    [
        "/api/wallets/transfers",
        "/api/admin/wallets/",
        "/api/admin/journal-entries/",
        "/api/admin/transfers/"
    ];
}