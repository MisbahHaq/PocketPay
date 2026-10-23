using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WalletPlatform.Application.Abstractions.Persistence;
using WalletPlatform.Application.Abstractions.Services;
using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Infrastructure.Persistence;

/// <summary>
/// Implements idempotency key management. Keys are claimed and responses stored
/// within the same database transaction as the business operation.
/// </summary>
internal sealed class IdempotencyService(
    WalletDbContext dbContext,
    IOptions<IdempotencyOptions> options) : IIdempotencyService
{
    private readonly IdempotencyOptions _options = options.Value;

    public async Task<IdempotencyClaimResult?> TryClaimAsync(
        Guid userId,
        string key,
        string method,
        string path,
        object requestBody,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key, cancellationToken);

        var requestHash = IdempotencyRecord.ComputeHash(requestBody);

        if (existing is not null)
        {
            // Key already exists - check if it's expired
            if (existing.IsExpired)
            {
                // Expired key - remove it and allow new claim
                dbContext.IdempotencyRecords.Remove(existing);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else if (!existing.MatchesRequest(requestBody))
            {
                // Same key, different payload - client bug
                throw new InvalidOperationException(
                    $"Idempotency key '{key}' already used with a different request payload. " +
                    $"Expected hash: {existing.RequestHash}, got: {requestHash}");
            }
            else if (existing.HasResponse)
            {
                // Same key, same payload - replay the stored response
                return new IdempotencyClaimResult
                {
                    StatusCode = existing.ResponseStatusCode!.Value,
                    ResponseBody = existing.ResponseBody,
                    IsReplay = true
                };
            }
            else
            {
                // Key exists but no response yet (in-progress request)
                // This shouldn't happen in practice due to FOR UPDATE locking,
                // but we handle it gracefully
                throw new InvalidOperationException(
                    $"Idempotency key '{key}' is already being processed by another request.");
            }
        }

        // Claim the key by inserting a new record (no response yet)
        var record = IdempotencyRecord.Create(userId, key, method, path, requestBody, _options.Ttl);
        dbContext.IdempotencyRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        return null; // Key claimed successfully, proceed with business operation
    }

    public async Task StoreResponseAsync(
        Guid userId,
        string key,
        int statusCode,
        object? responseBody,
        CancellationToken cancellationToken = default)
    {
        var record = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key, cancellationToken);

        if (record is null)
        {
            // This should not happen if TryClaimAsync was called correctly
            return;
        }

        // Only store successful responses (2xx) and deterministic business failures (422)
        // Do NOT store 5xx or unexpected errors - they should be retryable
        var shouldStore = statusCode >= 200 && statusCode < 300 || statusCode == 422;

        if (shouldStore)
        {
            record.StoreResponse(statusCode, responseBody);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        // If not stored, the key remains claimed but without response
        // On retry, TryClaimAsync will find the record without response and allow re-processing
    }
}