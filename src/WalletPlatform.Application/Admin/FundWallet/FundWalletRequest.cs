namespace WalletPlatform.Application.Admin.FundWallet;

/// <summary>
/// Request to simulate a deposit: the platform's funding account is debited and the
/// target wallet credited. Amount is in minor units (paisa) and must be positive.
/// The wallet id comes from the route, not the body.
/// </summary>
public sealed record FundWalletRequest(long AmountMinorUnits);
