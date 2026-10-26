using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletPlatform.Api.Authorization;
using WalletPlatform.Application.Reconciliation;
using WalletPlatform.Infrastructure.Data;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/reconciliation")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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
}

public sealed record CreateReconciliationRunRequest(string BillerCode, DateOnly BusinessDate);
