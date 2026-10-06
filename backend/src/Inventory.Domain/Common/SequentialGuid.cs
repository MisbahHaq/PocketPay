using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Inventory.Domain.Common;

/// <summary>
/// Generates RFC 9562 version 7 UUIDs (time-ordered).
/// </summary>
/// <remarks>
/// <para>
/// Why not <c>Guid.NewGuid()</c>? A v4 UUID is 122 random bits, so consecutive
/// inserts land at random positions in the B-tree. On PostgreSQL that means a
/// steadily-updating table (our stock movement ledger is the worst case: append
/// heavy, forever) splits index pages continuously, and both writes and reads
/// degrade as the table grows.
/// </para>
/// <para>
/// A v7 UUID puts a 48-bit Unix millisecond timestamp in the high bits, so keys
/// sort in creation order. Index locality stays good, and the timestamp is free
/// for debugging without adding an <c>ORDER BY</c>.
/// </para>
/// <para>
/// This is a deliberate, isolated dependency-free choice. If we ever move to a
/// database with a native ordered GUID type, only this class changes.
/// </para>
/// </remarks>
public static class SequentialGuid
{
    /// <summary>Number of ids we can hand out before reusing a millisecond timestamp.</summary>
    private const int MaxSequence = 0x0FFF;

    private static readonly object Sync = new();
    private static long _lastTimestamp = -1;
    private static int _sequence;

    /// <summary>Creates a new time-ordered (v7) UUID.</summary>
    public static Guid New() => New(DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a new time-ordered (v7) UUID for an explicit instant. Exposed so the
    /// monotonicity guarantee can be tested without sleeping.
    /// </summary>
    public static Guid New(DateTimeOffset instant)
    {
        var timestamp = instant.ToUnixTimeMilliseconds();
        int sequence;

        lock (Sync)
        {
            if (timestamp > _lastTimestamp)
            {
                _lastTimestamp = timestamp;
                _sequence = 0;
            }
            else if (_sequence >= MaxSequence)
            {
                // More than 4096 ids were requested within one millisecond. Push the
                // logical clock forward instead of risk emitting a duplicate key -
                // a PK collision is a far worse failure than a few microseconds of delay.
                _lastTimestamp++;
                _sequence = 0;
            }

            sequence = _sequence++;
        }

        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);

        Span<byte> wide = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(wide, timestamp);

        // 48-bit big-endian Unix milliseconds into bytes 0..5. The value is written as a
        // full 8-byte big-endian integer, so the top 6 bytes are the 48 significant bits.
        wide[2..8].CopyTo(bytes[0..6]);

        // rand_a (12 bits) is spent entirely on the sequence number, which is what
        // guarantees ids created inside the same millisecond still sort correctly.
        bytes[6] = (byte)(0x70 | ((sequence >> 8) & 0x0F)); // version 7 = 0111
        bytes[7] = (byte)(sequence & 0xFF);

        // RFC 9562 variant 2 = binary 10 in the two high bits of byte 8.
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        // bigEndian: true because PostgreSQL stores uuid in RFC network byte order,
        // so this keeps the in-memory Guid and the stored uuid byte-identical.
        return new Guid(bytes, bigEndian: true);
    }
}