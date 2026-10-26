using System.Security.Cryptography;
using System.Text;

namespace WalletPlatform.Application.Reconciliation;

public sealed record SettlementFileHeader(string Version, string BillerCode, string BusinessDate, string GeneratedAt);
public sealed record SettlementDetailRow(string BillerReference, string ClientReference, string ConsumerNumber, long AmountMinorUnits, string Currency, string Status, string ProcessedAt);
public sealed record SettlementTrailer(int RowCount, long TotalAmount, string Hash);

public sealed record ParsedSettlementFile(SettlementFileHeader Header, IReadOnlyList<SettlementDetailRow> Details, SettlementTrailer Trailer);

public static class SettlementFileValidator
{
    public static string? Validate(ParsedSettlementFile file, out string reason)
    {
        reason = "";
        // header version
        if (file.Header.Version != "1") { reason = "bad header version"; return null; }
        // trailer row count
        if (file.Trailer.RowCount != file.Details.Count) { reason = "trailer row count mismatch"; return null; }
        // trailer total
        long total = file.Details.Where(d => d.Status == "Success").Sum(d => d.AmountMinorUnits);
        if (file.Trailer.TotalAmount != total) { reason = "trailer total mismatch"; return null; }
        // trailer hash
        var lines = file.Details.Select(d => $"{d.BillerReference},{d.ClientReference},{d.ConsumerNumber},{d.AmountMinorUnits},{d.Currency},{d.Status},{d.ProcessedAt}");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)))).ToLowerInvariant();
        if (!string.Equals(hash, file.Trailer.Hash, StringComparison.OrdinalIgnoreCase)) { reason = "trailer hash mismatch"; return null; }
        // amounts positive
        if (file.Details.Any(d => d.AmountMinorUnits <= 0)) { reason = "non-positive amount"; return null; }
        // currency known
        if (file.Details.Any(d => d.Currency != "PKR")) { reason = "unknown currency"; return null; }
        return file.Header.BillerCode;
    }
}
