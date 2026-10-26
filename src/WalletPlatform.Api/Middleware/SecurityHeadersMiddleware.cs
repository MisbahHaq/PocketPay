namespace WalletPlatform.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["X-Frame-Options"] = "DENY";
        ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
        ctx.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        if (!env.IsDevelopment())
            ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        ctx.Response.Headers.Remove("Server");
        await next(ctx);
    }
}
