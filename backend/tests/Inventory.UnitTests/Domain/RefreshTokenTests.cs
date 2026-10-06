using Inventory.Domain.Common;
using Inventory.Domain.Identity;

namespace Inventory.UnitTests.Domain;

/// <summary>
/// Refresh token rotation and replay detection. This is the code that stands between a
/// stolen session and a compromised account, so the edge cases are pinned deliberately.
/// </summary>
public sealed class RefreshTokenTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_returns_a_raw_token_and_stores_only_its_hash()
    {
        var (token, rawToken) = RefreshToken.Create(UserId, null, Now);

        rawToken.Should().NotBeNullOrWhiteSpace();

        // The raw value must never be recoverable from the row.
        token.TokenHash.Should().NotBe(rawToken);
        token.TokenHash.Should().Be(RefreshToken.HashToken(rawToken));
    }

    [Fact]
    public void Hashing_is_deterministic_so_the_token_can_be_found_by_hash()
    {
        RefreshToken.HashToken("abc").Should().Be(RefreshToken.HashToken("abc"));
        RefreshToken.HashToken("abc").Should().NotBe(RefreshToken.HashToken("abd"));
    }

    [Fact]
    public void Create_starts_a_new_family_when_none_is_supplied()
    {
        var (first, _) = RefreshToken.Create(UserId, null, Now);
        var (second, _) = RefreshToken.Create(UserId, null, Now);

        first.FamilyId.Should().NotBe(second.FamilyId);
    }

    [Fact]
    public void Create_continues_the_family_when_one_is_supplied()
    {
        var familyId = Guid.NewGuid();

        var (rotated, _) = RefreshToken.Create(UserId, familyId, Now);

        rotated.FamilyId.Should().Be(familyId);
    }

    [Fact]
    public void A_new_token_is_active_and_unexpired()
    {
        var (token, _) = RefreshToken.Create(UserId, null, Now);

        token.IsActive(Now).Should().BeTrue();
        token.IsRevoked.Should().BeFalse();
        token.IsReplayed.Should().BeFalse();
    }

    [Fact]
    public void A_token_is_inactive_once_past_its_expiry()
    {
        var (token, _) = RefreshToken.Create(UserId, null, Now, lifetimeDays: 7);

        token.IsActive(Now.AddDays(6)).Should().BeTrue();
        token.IsExpired(Now.AddDays(7)).Should().BeTrue();
        token.IsActive(Now.AddDays(7)).Should().BeFalse();
    }

    [Fact]
    public void Rotated_by_replacement_marks_the_token_as_a_replay_candidate()
    {
        var (token, _) = RefreshToken.Create(UserId, null, Now);

        token.RevokedByRotation("replacement-hash", Now);

        token.IsRevoked.Should().BeTrue();
        token.IsActive(Now).Should().BeFalse();

        // This flag is what makes a second presentation detectable as theft rather than
        // a benign double-submit.
        token.IsReplayed.Should().BeTrue();
        token.ReplacedByTokenHash.Should().Be("replacement-hash");
    }

    [Fact]
    public void Revoke_for_an_explicit_reason_is_not_treated_as_a_replay()
    {
        var (token, _) = RefreshToken.Create(UserId, null, Now);

        token.Revoke(Now, RefreshTokenRevocationReasons.Logout);

        token.IsRevoked.Should().BeTrue();
        token.RevokedReason.Should().Be(RefreshTokenRevocationReasons.Logout);

        // Signing out must never trigger compromise detection, or every logout would
        // look like an attack.
        token.IsReplayed.Should().BeFalse();
    }

    [Fact]
    public void Revoke_does_not_overwrite_an_earlier_reason()
    {
        var (token, _) = RefreshToken.Create(UserId, null, Now);

        token.Revoke(Now, RefreshTokenRevocationReasons.Logout);
        token.Revoke(Now.AddMinutes(1), RefreshTokenRevocationReasons.Administrative);

        // Keeping the first reason preserves what actually happened.
        token.RevokedReason.Should().Be(RefreshTokenRevocationReasons.Logout);
        token.RevokedAt.Should().Be(Now);
    }

    [Fact]
    public void Rotation_does_not_overwrite_an_earlier_revocation_reason()
    {
        var (token, _) = RefreshToken.Create(UserId, null, Now);
        token.Revoke(Now, RefreshTokenRevocationReasons.Administrative);

        token.RevokedByRotation("replacement-hash", Now.AddSeconds(1));

        token.RevokedReason.Should().Be(RefreshTokenRevocationReasons.Administrative);
    }

    [Fact]
    public void Create_rejects_an_empty_user_id()
    {
        var act = () => RefreshToken.Create(Guid.Empty, null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HashToken_rejects_an_empty_value()
    {
        var act = () => RefreshToken.HashToken("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Raw_tokens_use_a_url_safe_encoding()
    {
        var (_, rawToken) = RefreshToken.Create(UserId, null, Now);

        // Base64url, so the value survives being put in a cookie, a query string or JSON
        // without escaping.
        rawToken.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
    }

    [Fact]
    public void Distinct_creations_produce_distinct_raw_tokens()
    {
        var tokens = Enumerable.Range(0, 1_000)
            .Select(_ => RefreshToken.Create(UserId, null, Now).RawToken)
            .ToList();

        tokens.Should().OnlyHaveUniqueItems();
    }
}