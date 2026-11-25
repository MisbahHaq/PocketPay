using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data.Entities;

namespace WalletPlatform.Infrastructure.Reconciliation;

/// <summary>
/// Parses the settlement CSV produced by the mock biller:
/// HEADER,... / DETAIL,... / TRAILER,count,total,hash
/// The trailer hash is SHA256 over the detail payloads joined with LF; it detects
/// truncated or tampered files before they are imported.
/// </summary>
internal static class SettlementCsvParser
{
    public sealed record ParseResult(SettlementFile File, IReadOnlyList<SettlementRow> Rows);

    public static ParseResult Parse(string billerCode, DateOnly businessDate, string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            throw new ArgumentException("Settlement file content is empty.", nameof(csvContent));
        }

        var lines = csvContent.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            throw new ArgumentException("Settlement file must contain a header and trailer.", nameof(csvContent));
        }

        if (!lines[0].StartsWith("HEADER,", StringComparison.Ordinal))
        {
            throw new ArgumentException("Settlement file is missing its HEADER row.", nameof(csvContent));
        }

        var trailer = lines[^1].Split(',');
        if (trailer.Length < 4 || trailer[0] != "TRAILER" ||
            !int.TryParse(trailer[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var declaredCount) ||
            !long.TryParse(trailer[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var declaredTotal))
        {
            throw new ArgumentException("Settlement file has an invalid TRAILER row.", nameof(csvContent));
        }

        var detailPayloads = lines[1..^1]
            .Where(line => line.StartsWith("DETAIL,", StringComparison.Ordinal))
            .Select(line => line["DETAIL,".Length..])
            .ToList();

        if (detailPayloads.Count != declaredCount)
        {
            throw new ArgumentException(
                $"Settlement row count mismatch: expected {declaredCount}, found {detailPayloads.Count}.",
                nameof(csvContent));
        }

        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", detailPayloads)))).ToLowerInvariant();
        if (!string.Equals(trailer[3], expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Settlement file hash does not match its contents.", nameof(csvContent));
        }

        var rows = new List<SettlementRow>(detailPayloads.Count);
        long actualTotal = 0;

        foreach (var payload in detailPayloads)
        {
            var fields = payload.Split(',');
            if (fields.Length < 7)
            {
                throw new ArgumentException($"Malformed DETAIL row: {payload}", nameof(csvContent));
            }

            if (!long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
            {
                throw new ArgumentException($"Invalid amount in DETAIL row: {payload}", nameof(csvContent));
            }

            if (!DateTimeOffset.TryParse(fields[6], CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var processedAt))
            {
                processedAt = DateTimeOffset.UtcNow;
            }

            var status = fields[5];
            rows.Add(new SettlementRow
            {
                BillerReference = fields[0],
                ClientReference = fields[1],
                ConsumerNumber = fields[2],
                AmountMinorUnits = amount,
                Currency = fields[4],
                Status = status,
                ProcessedAt = processedAt,
            });

            if (string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase))
            {
                actualTotal += amount;
            }
        }

        if (actualTotal != declaredTotal)
        {
            throw new ArgumentException(
                $"Settlement total mismatch: expected {declaredTotal}, found {actualTotal}.",
                nameof(csvContent));
        }

        var file = new SettlementFile
        {
            BillerCode = billerCode,
            BusinessDate = businessDate,
            FileHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(csvContent))).ToLowerInvariant(),
            RawContent = csvContent,
            RowCount = rows.Count,
            TotalAmount = actualTotal,
            ImportedAt = DateTimeOffset.UtcNow,
            Status = SettlementFileStatus.Imported,
        };

        foreach (var row in rows)
        {
            row.SettlementFileId = file.Id;
        }

        return new ParseResult(file, rows);
    }
}
