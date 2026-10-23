namespace WalletPlatform.Application.Abstractions.Services;

/// <summary>
/// Service for managing idempotency keys. The key is claimed in the SAME transaction
/// as the business operation. If the operation succeeds, the response is stored.
/// If it fails with a business error (422), the error is stored. If it crashes (5xx),
/// the key is NOT stored, allowing retry.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Tries to claim an idempotency key for the current request.
    /// Returns the stored response if the key was already used with the same payload.
    /// Returns null if the key is available and has been claimed.
    /// Throws if the key exists with a DIFFERENT payload (client bug).
    /// </summary>
    Task<IdempotencyClaimResult?> TryClaimAsync(
        Guid userId,
        string key,
        string method,
        string path,
        object requestBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the response for a claimed idempotency key.
    /// Must be called in the SAME transaction as the business operation.
    /// </summary>
    Task StoreResponseAsync(
        Guid userId,
        string key,
        int statusCode,
        object? responseBody,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of trying to claim an idempotency key.
/// </summary>
public sealed record IdempotencyClaimResult
{
    public int StatusCode { get; init; }
    public string? ResponseBody { get; init; }
    public bool IsReplay { get; init; }
}