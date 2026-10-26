using System.Text.RegularExpressions;

namespace WalletPlatform.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private static readonly Regex Valid = new("^[A-Za-z0-9\\-]{1,64}$", RegexOptions.Compiled);

    public async Task InvokeAsync(HttpContext ctx)
    {
        var incoming = ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var v) ? v.ToString() : null;
        var id = (incoming is not null && Valid.IsMatch(incoming)) ? incoming : Guid.NewGuid().ToString("N");
        ctx.Response.Headers["X-Correlation-Id"] = id;
        ctx.Items["CorrelationId"] = id;
        await next(ctx);
    }
}
