using Microsoft.AspNetCore.Mvc;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/reconciliation")]
public sealed class ReconciliationController : ControllerBase
{
    [HttpGet("runs")]
    public IActionResult GetRuns() => Ok(Array.Empty<object>());
}
