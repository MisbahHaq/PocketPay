using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletPlatform.Api.Authorization;
using WalletPlatform.Application.Reconciliation;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/reconciliation")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class ReconciliationController(IReconciliationService reconciliationService) : ControllerBase
{
    [HttpGet("runs")]
    public IActionResult GetRuns() => Ok(Array.Empty<object>());

    [HttpPost("runs")]
    public async Task<IActionResult> CreateRun([FromBody] CreateReconciliationRunRequest request, CancellationToken ct)
    {
        var runId = await reconciliationService.RunAsync(request.BillerCode, request.BusinessDate, ct);
        return Accepted(new { runId });
    }
}

public sealed record CreateReconciliationRunRequest(string BillerCode, DateOnly BusinessDate);
