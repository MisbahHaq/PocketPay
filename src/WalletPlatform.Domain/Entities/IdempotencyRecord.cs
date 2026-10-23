using System.Security.Cryptography;
using System.Text.Json;
using WalletPlatform.Domain.Exceptions;

namespace WalletPlatform.Domain.Entities;

/// <summary>
/// Represents an idempotency key claim for a specific request.
/// Stored in the same transaction as the business operation to ensure atomicity:
/// - If the operation succeeds, the response is stored with the key
/// - If the operation fails with a business error (422), the error response is stored
/// - If the operation crashes (5xx), the key is NOT stored (allowing retry)
/// 
/// The key is scoped per user, so one user's key can never collide with another's.
/// </summary>
public sealed class IdempotencyRecord
{
    private IdempotencyRecord() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Method { get; private set; } = null!;
    public string Path { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public static IdempotencyRecord Create(
        Guid userId,
        string key,
        string method,
        string path,
        object requestBody,
        TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(key));
        }

        if (key.Length < 16 || key.Length > 64)
        {
            throw new ArgumentException("Idempotency key must be between 16 and 64 characters.", nameof(key));
        }

        var requestHash = ComputeHash(requestBody);
        var now = DateTimeOffset.UtcNow;

        return new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Key = key,
            Method = method,
            Path = path,
            RequestHash = requestHash,
            ResponseStatusCode = null,
            ResponseBody = null,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl)
        };
    }

    public void StoreResponse(int statusCode, object? responseBody)
    {
        if (ResponseStatusCode is not null)
        {
            throw new InvalidOperationException("Response already stored for this idempotency key.");
        }

        ResponseStatusCode = statusCode;
        ResponseBody = responseBody is null ? null : JsonSerializer.Serialize(responseBody);
    }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    public bool HasResponse => ResponseStatusCode is not null;

    public bool MatchesRequest(object requestBody) => RequestHash == ComputeHash(requestBody);

    public static string ComputeHash(object requestBody)
    {
        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }
}