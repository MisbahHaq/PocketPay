using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Domain.Entities;

public sealed class Wallet
{
    private Wallet()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string WalletNumber { get; private set; } = null!;
    public Currency Currency { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    // NOTE: There is intentionally NO balance column. A wallet's balance is derived
    // from double-entry ledger entries (introduced in Week 3); storing a mutable
    // balance alongside the ledger would allow the two sources of truth to drift.

    public static Wallet Create(Guid userId, string walletNumber, Currency currency, DateTimeOffset createdAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (walletNumber is not { Length: 12 } || !walletNumber.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Wallet number must be exactly 12 digits.", nameof(walletNumber));
        }

        return new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WalletNumber = walletNumber,
            Currency = currency,
            CreatedAtUtc = createdAtUtc,
        };
    }
}
