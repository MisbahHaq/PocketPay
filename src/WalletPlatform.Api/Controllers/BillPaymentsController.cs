using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Data;
using WalletPlatform.Infrastructure.Services;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Route("api/bill-payments")]
[Authorize]
[EnableRateLimiting("read")]
public class BillPaymentsController(
    BillPaymentService svc,
    WalletDbContext db) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("money")]
    public async Task<IActionResult> Create([FromBody] CreateBillPaymentRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var payment = await svc.AcceptAsync(req.WalletId, req.BillerCode, req.ConsumerNumber, req.AmountMinorUnits, userId, ct);
        return Accepted(new { payment.Id, payment.Status, payment.Amount.MinorUnits, payment.Amount.Currency });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var safePage = page <= 0 ? 1 : page;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var query = db.BillPayments
            .AsNoTracking()
            .Join(db.Wallets, p => p.WalletId, w => w.Id, (p, w) => new { Payment = p, w.UserId })
            .Where(x => x.UserId == userId)
            .Select(x => x.Payment);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Domain.Enums.BillPaymentStatus>(status, true, out var parsed))
        {
            query = query.Where(p => p.Status == parsed);
        }

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(p => new BillPaymentSummary(
                p.Id, p.WalletId, p.BillerCode, p.ConsumerNumber,
                p.Amount.MinorUnits, p.Amount.Currency, p.Status, p.BillerReference,
                p.FailureReason, p.CreatedAt, p.UpdatedAt))
            .ToListAsync(ct);

        return Ok(new PagedResult<BillPaymentSummary>(items, (int)Math.Min(total, int.MaxValue), safePage, safePageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var payment = await db.BillPayments
            .AsNoTracking()
            .Join(db.Wallets, p => p.WalletId, w => w.Id, (p, w) => new { Payment = p, w.UserId })
            .Where(x => x.UserId == userId && x.Payment.Id == id)
            .Select(x => x.Payment)
            .SingleOrDefaultAsync(ct);

        if (payment is null)
        {
            return NotFound();
        }

        return Ok(new BillPaymentDetail(
            payment.Id, payment.WalletId, payment.BillerCode, payment.ConsumerNumber,
            payment.Amount.MinorUnits, payment.Amount.Currency, payment.Status,
            payment.FailureReason, payment.BillerReference, payment.JournalEntryId,
            payment.RefundJournalEntryId, payment.AttemptCount,
            payment.CreatedAt, payment.UpdatedAt));
    }
}

public record CreateBillPaymentRequest(Guid WalletId, string BillerCode, string ConsumerNumber, long AmountMinorUnits);

public record BillPaymentSummary(
    Guid Id, Guid WalletId, string BillerCode, string ConsumerNumber,
    long AmountMinorUnits, Domain.Enums.Currency Currency, Domain.Enums.BillPaymentStatus Status,
    string? BillerReference, string? FailureReason, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record BillPaymentDetail(
    Guid Id, Guid WalletId, string BillerCode, string ConsumerNumber,
    long AmountMinorUnits, Domain.Enums.Currency Currency, Domain.Enums.BillPaymentStatus Status,
    string? FailureReason, string? BillerReference, Guid? JournalEntryId, Guid? RefundJournalEntryId,
    int AttemptCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
