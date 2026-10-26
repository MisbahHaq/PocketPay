namespace WalletPlatform.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var id = ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var v) && v.ToString().Length <= 64
            ? v.ToString() : Guid.NewGuid().ToString("N");
        ctx.Response.Headers["X-Correlation-Id"] = id;
        await next(ctx);
    }
}
