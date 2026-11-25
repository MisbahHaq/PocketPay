using System.Data;
using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Audit;
using WalletPlatform.Application.Reconciliation;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Domain.Enums;
using WalletPlatform.Infrastructure.Data;
using WalletPlatform.Infrastructure.Data.Entities;

namespace WalletPlatform.Infrastructure.Reconciliation;

public sealed class ReconciliationService(
    WalletDbContext db,
    IAuditService audit) : IReconciliationService
{
    // Asia/Karachi has a fixed UTC+5 offset (no DST since 2009).
    private static readonly TimeSpan PakistanOffset = TimeSpan.FromHours(5);

    public async Task<Guid> ImportSettlementFileAsync(
        string billerCode,
        DateOnly businessDate,
        string csvContent,
        CancellationToken ct = default)
    {
        var parsed = SettlementCsvParser.Parse(billerCode, businessDate, csvContent);

        var duplicate = await db.SettlementFiles
            .AnyAsync(f => f.BillerCode == billerCode && f.BusinessDate == businessDate &&
                           f.FileHash == parsed.File.FileHash, ct);
        if (duplicate)
        {
            return (await db.SettlementFiles
                .Where(f => f.BillerCode == billerCode && f.BusinessDate == businessDate &&
                            f.FileHash == parsed.File.FileHash)
                .Select(f => f.Id)
                .SingleAsync(ct));
        }

        db.SettlementFiles.Add(parsed.File);
        db.SettlementRows.AddRange(parsed.Rows);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            "Reconciliation.SettlementImport", "SettlementFile", parsed.File.Id.ToString(), "Imported",
            new { billerCode, businessDate, parsed.File.RowCount, parsed.File.TotalAmount }, ct);

        return parsed.File.Id;
    }

    public async Task<Guid> RunAsync(
        string billerCode,
        DateOnly businessDate,
        CancellationToken ct = default)
    {
        var file = await db.SettlementFiles
            .Where(f => f.BillerCode == billerCode && f.BusinessDate == businessDate &&
                        f.Status == SettlementFileStatus.Imported)
            .OrderByDescending(f => f.ImportedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                $"No imported settlement file for {billerCode} on {businessDate:yyyy-MM-dd}.");

        var startUtc = new DateTimeOffset(businessDate.ToDateTime(TimeOnly.MinValue), PakistanOffset).UtcDateTime;
        var endUtc = startUtc.AddDays(1);

        var run = new ReconciliationRun
        {
            Id = Guid.NewGuid(),
            BillerCode = billerCode,
            BusinessDate = businessDate,
            SettlementFileId = file.Id,
            Status = ReconciliationRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            Trigger = ReconciliationTrigger.Manual,
        };
        db.ReconciliationRuns.Add(run);
        await db.SaveChangesAsync(ct);

        // Our side: completed bill payments for this biller in the business date window.
        var ourPayments = await db.BillPayments
            .AsNoTracking()
            .Where(p => p.BillerCode == billerCode &&
                        p.Status == BillPaymentStatus.Completed &&
                        p.CreatedAt >= startUtc && p.CreatedAt < endUtc)
            .Select(p => new
            {
                ClientReference = p.Id.ToString(),
                p.Id,
                OurAmount = p.Amount.MinorUnits,
                OurStatus = p.Status.ToString(),
            })
            .ToListAsync(ct);

        // Biller side: settlement rows, with duplicate counts as the matching SQL specifies.
        var billerRows = await db.SettlementRows
            .AsNoTracking()
            .Where(r => r.SettlementFileId == file.Id)
            .Select(r => new
            {
                r.ClientReference,
                r.BillerReference,
                BillerAmount = r.AmountMinorUnits,
                BillerStatus = r.Status,
                DuplicateCount = db.SettlementRows.Count(x =>
                    x.SettlementFileId == file.Id && x.ClientReference == r.ClientReference),
            })
            .ToListAsync(ct);

        var ourByReference = ourPayments
            .GroupBy(p => p.ClientReference)
            .ToDictionary(g => g.Key, g => g.ToList());
        var billerByReference = billerRows
            .GroupBy(r => r.ClientReference)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = new List<ReconciliationItem>();
        var references = ourByReference.Keys.Concat(billerByReference.Keys).Distinct();

        foreach (var reference in references)
        {
            ourByReference.TryGetValue(reference, out var ours);
            billerByReference.TryGetValue(reference, out var biller);

            if (ours is null || biller is null)
            {
                var itemCategory = ReconciliationMatcher.Categorize(
                    ours is not null, biller is not null,
                    ours?.FirstOrDefault()?.OurAmount ?? 0,
                    biller?.FirstOrDefault()?.BillerAmount ?? 0,
                    biller?.FirstOrDefault()?.BillerStatus,
                    biller?.Count ?? 0);
                var ourFirst = ours?.FirstOrDefault();
                var billerFirst = biller?.FirstOrDefault();
                items.Add(new ReconciliationItem
                {
                    RunId = run.Id,
                    ClientReference = reference,
                    PaymentId = ourFirst?.Id,
                    Category = itemCategory,
                    OurAmount = ourFirst?.OurAmount ?? 0,
                    BillerAmount = billerFirst?.BillerAmount ?? 0,
                    OurStatus = ourFirst?.OurStatus,
                    BillerStatus = billerFirst?.BillerStatus,
                    ResolutionNote = biller is { Count: > 1 }
                        ? $"Biller reported {biller.Count} rows for this reference."
                        : null,
                });
                continue;
            }

            var ourPayment = ours.First();
            var billerRow = biller.First();
            var category = ReconciliationMatcher.Categorize(
                true, true, ourPayment.OurAmount, billerRow.BillerAmount,
                billerRow.BillerStatus, biller.Count);
            var isReconciled = category == ReconciliationMatcher.Matched;

            items.Add(new ReconciliationItem
            {
                RunId = run.Id,
                ClientReference = reference,
                PaymentId = ourPayment.Id,
                Category = category,
                OurAmount = ourPayment.OurAmount,
                BillerAmount = billerRow.BillerAmount,
                OurStatus = ourPayment.OurStatus,
                BillerStatus = billerRow.BillerStatus,
                ResolutionNote = biller.Count > 1
                    ? $"Biller reported {biller.Count} rows for this reference."
                    : !string.Equals(billerRow.BillerStatus, "Success", StringComparison.OrdinalIgnoreCase)
                        ? $"Biller status: {billerRow.BillerStatus}." : null,
            });

            var reconciliationStatus = isReconciled ? "Reconciled" : "Mismatch";
            var reconciliation = await db.PaymentReconciliations
                .SingleOrDefaultAsync(x => x.PaymentId == ourPayment.Id, ct);
            if (reconciliation is null)
            {
                db.PaymentReconciliations.Add(new PaymentReconciliation
                {
                    PaymentId = ourPayment.Id,
                    Status = reconciliationStatus,
                    RunId = run.Id,
                });
            }
            else
            {
                reconciliation.Status = reconciliationStatus;
                reconciliation.RunId = run.Id;
            }
        }

        db.ReconciliationItems.AddRange(items);

        run.MatchedCount = items.Count(i => i.Category == "Matched");
        run.MissingAtBillerCount = items.Count(i => i.Category == "MissingAtBiller");
        run.UnknownAtOurSideCount = items.Count(i => i.Category == "UnknownAtOurSide");
        run.AmountMismatchCount = items.Count(i => i.Category == "AmountMismatch");
        run.Status = ReconciliationRunStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            "Reconciliation.Run", "ReconciliationRun", run.Id.ToString(), "Completed",
            new
            {
                billerCode,
                businessDate,
                run.MatchedCount,
                run.MissingAtBillerCount,
                run.UnknownAtOurSideCount,
                run.AmountMismatchCount,
            },
            ct);

        return run.Id;
    }

    public async Task<IReadOnlyList<ReconciliationItemDto>> GetItemsAsync(Guid runId, CancellationToken ct = default)
    {
        return await db.ReconciliationItems
            .AsNoTracking()
            .Where(i => i.RunId == runId)
            .OrderBy(i => i.ClientReference)
            .Select(i => new ReconciliationItemDto(
                i.Id, i.RunId, i.ClientReference, i.PaymentId, i.Category,
                i.OurAmount, i.BillerAmount, i.OurStatus, i.BillerStatus, i.Resolution))
            .ToListAsync(ct);
    }
}
