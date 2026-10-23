namespace WalletPlatform.Application.Common;

public enum BillerOutcome { Success, Declined, Unknown }

public sealed record BillerResult(BillerOutcome Outcome, string? BillerReference, string? DeclineCode, string? Reason);
