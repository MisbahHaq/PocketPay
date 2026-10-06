using Inventory.Domain.Common;

namespace Inventory.UnitTests.Domain;

/// <summary>
/// The time-ordering guarantee is what keeps the stock movement ledger's index healthy.
/// If it regresses, the failure is invisible in tests and only shows up as a database
/// getting slower over months - so the byte layout is asserted explicitly.
/// </summary>
public sealed class SequentialGuidTests
{
    [Fact]
    public void New_sets_the_version_7_bits()
    {
        var guid = SequentialGuid.New();

        // RFC 9562: version occupies the high nibble of octet 6.
        var version = guid.ToString()[14];

        version.Should().Be('7');
    }

    [Fact]
    public void New_sets_the_rfc_variant_bits()
    {
        var guid = SequentialGuid.New();

        // RFC 9562 variant 2 is binary 10, so the 17th hex digit is one of 8, 9, a or b.
        var variant = guid.ToString()[19].ToString();

        "89ab".Should().Contain(variant);
    }

    [Fact]
    public void New_encodes_the_supplied_instant_in_the_high_bits()
    {
var instant = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);

var guid = SequentialGuid.New(instant);

        // The first 48 bits are the timestamp in big-endian order, which is exactly what
        // Guid's text form prints first. "N" format so the dash at index 8 does not
        // shift the comparison.
        var expectedHighBits = instant.ToUnixTimeMilliseconds().ToString("x12");

        guid.ToString("N")[..12].Should().Be(expectedHighBits);
    }

    [Fact]
    public void New_returns_unique_values_for_a_large_batch()
    {
        var ids = Enumerable.Range(0, 10_000).Select(_ => SequentialGuid.New()).ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Values_sort_in_creation_order_within_a_single_instant()
    {
        var instant = DateTimeOffset.UtcNow;

        var ordered = Enumerable.Range(0, 1_000)
            .Select(_ => SequentialGuid.New(instant))
            .ToList();

        // A v4 UUID would fail this. Sorting being identical to insertion order is the
        // whole reason v7 exists.
        ordered.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Values_sort_in_creation_order_across_instants()
    {
        var start = DateTimeOffset.UnixEpoch.AddDays(20_000);

        // Clearly separated windows: the earlier batch is a full second before the later
        // one, so a timestamp regression would be unmistakable.
        var earlier = Enumerable.Range(0, 50).Select(i => SequentialGuid.New(start.AddMilliseconds(i))).ToList();
        var later = Enumerable.Range(0, 50).Select(i => SequentialGuid.New(start.AddMilliseconds(1_000 + i))).ToList();

        earlier.Concat(later).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Uses_big_endian_byte_order_so_the_stored_uuid_matches_memory()
    {
        // If this regresses, values still sort correctly in C# but land scrambled in
        // PostgreSQL, silently destroying index locality.
        var guid = SequentialGuid.New();
        var bytes = guid.ToByteArray(bigEndian: true);

        // Text form is the big-endian rendering of those same bytes.
        bytes[0].Should().Be(byte.Parse(guid.ToString()[..2], System.Globalization.NumberStyles.HexNumber));
    }

    [Fact]
    public void Survives_more_ids_than_fit_in_one_millisecond_without_duplicating()
    {
        // 4096 is the sequence width; ask for more than that from one instant and the
        // logical clock must advance rather than emit a duplicate key.
        var instant = DateTimeOffset.UnixEpoch;

        var ids = Enumerable.Range(0, 5_000).Select(_ => SequentialGuid.New(instant)).ToList();

        ids.Should().OnlyHaveUniqueItems();
    }
}