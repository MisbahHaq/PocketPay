using WalletPlatform.Application.Common;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Transfers.Policies;

/// <summary>
/// Base result for a policy check. A policy either passes (Success) or fails with a
/// machine-readable code and a human-readable message.
/// </summary>
public readonly record struct PolicyResult
{
    public bool IsSuccess { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }

    public static PolicyResult Success() => new() { IsSuccess = true };

    public static PolicyResult Fail(string code, string message) =>
        new() { IsSuccess = false, Code = code, Message = message };
}