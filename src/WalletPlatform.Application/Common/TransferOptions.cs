namespace WalletPlatform.Application.Common;

/// <summary>
/// Configuration for wallet-to-wallet transfers. All limits are in minor units (e.g. paisa
/// for PKR). Validate on startup via <see cref="TransferOptionsValidator"/>.
/// </summary>
public sealed class TransferOptions
{
    public const string SectionName = "Transfer";

    /// <summary>Flat fee charged per transfer, in minor units. Default: 0.</summary>
    public long FlatFeeMinorUnits { get; init; } = 0;

    /// <summary>Minimum transfer amount (inclusive), in minor units. Default: 1.</summary>
    public long MinAmountMinorUnits { get; init; } = 1;

    /// <summary>Maximum transfer amount (inclusive), in minor units. Default: 1_000_000_000 (10,000 major units).</summary>
    public long MaxAmountMinorUnits { get; init; } = 1_000_000_000;

    /// <summary>Maximum total outgoing amount per wallet in a rolling 24-hour window, in minor units. Default: 5_000_000_000 (50,000 major units).</summary>
    public long DailyOutgoingLimitMinorUnits { get; init; } = 5_000_000_000;
}