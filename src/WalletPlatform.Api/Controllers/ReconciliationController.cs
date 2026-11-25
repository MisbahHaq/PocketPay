using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WalletPlatform.Api.Authorization;
using WalletPlatform.Application.Reconciliation;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/reconciliation")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[EnableRateLimiting("admin")]
public sealed class ReconciliationController(
    IReconciliationService reconciliationService,
    WalletDbContext dbContext) : ControllerBase
{
    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns(CancellationToken ct)
    {
        var runs = await dbContext.ReconciliationRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(50)
            .ToListAsync(ct);
        return Ok(runs);
    }

    [HttpPost("runs")]
    public async Task<IActionResult> CreateRun([FromBody] CreateReconciliationRunRequest request, CancellationToken ct)
    {
        var runId = await reconciliationService.RunAsync(request.BillerCode, request.BusinessDate, ct);
        return Accepted(new { runId });
    }

    [HttpGet("runs/{runId:guid}/items")]
    public async Task<IActionResult> GetRunItems(Guid runId, CancellationToken ct)
    {
        var items = await reconciliationService.GetItemsAsync(runId, ct);
        return Ok(items);
    }

    [HttpPost("settlements/import")]
    public async Task<IActionResult> ImportSettlement([FromBody] ImportSettlementRequest request, CancellationToken ct)
    {
        var fileId = await reconciliationService.ImportSettlementFileAsync(
            request.BillerCode, request.BusinessDate, request.CsvContent, ct);
        return Ok(new { fileId });
    }

    [HttpGet("settlements")]
    public async Task<IActionResult> GetSettlements(
        [FromQuery] string? billerCode = null,
        [FromQuery] DateOnly? businessDate = null,
        CancellationToken ct = default)
    {
        var query = dbContext.SettlementFiles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(billerCode))
        {
            query = query.Where(f => f.BillerCode == billerCode);
        }
        if (businessDate.HasValue)
        {
            query = query.Where(f => f.BusinessDate == businessDate.Value);
        }

        var files = await query
            .OrderByDescending(f => f.ImportedAt)
            .Take(50)
            .ToListAsync(ct);
        return Ok(files);
    }
}

public sealed record CreateReconciliationRunRequest(string BillerCode, DateOnly BusinessDate);
public sealed record ImportSettlementRequest(string BillerCode, DateOnly BusinessDate, string CsvContent);
