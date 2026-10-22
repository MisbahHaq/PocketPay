namespace WalletPlatform.Infrastructure.Persistence;

/// <summary>
/// Controls startup seeding of system data. Enabled by default so a running app always
/// has its system ledger accounts; integration tests that manage migrations themselves
/// turn it off and seed explicitly.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool RunOnStartup { get; init; } = true;
}
