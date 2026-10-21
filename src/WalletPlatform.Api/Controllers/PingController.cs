using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PingController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PingResponse>(StatusCodes.Status200OK)]
    public ActionResult<PingResponse> Get()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version?.ToString(3) ?? "unknown";
        return Ok(new PingResponse(version, DateTime.UtcNow));
    }
}

public sealed record PingResponse(string Version, DateTime ServerTimeUtc);
