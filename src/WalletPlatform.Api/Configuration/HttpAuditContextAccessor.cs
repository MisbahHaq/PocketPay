using System.Security.Claims;
using WalletPlatform.Application.Audit;

namespace WalletPlatform.Api.Configuration;

/// <summary>
/// Captures audit actor information from the current HTTP request.
/// </summary>
internal sealed class HttpAuditContextAccessor(IHttpContextAccessor accessor) : IAuditContextAccessor
{
    public AuditContext Current
    {
        get
        {
            var http = accessor.HttpContext;
            if (http is null)
            {
                return AuditContext.Empty;
            }

            var user = http.User;
            Guid? actorUserId = null;
            if (Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? user.FindFirst("sub")?.Value, out var parsed))
            {
                actorUserId = parsed;
            }

            var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;

            return new AuditContext(
                actorUserId,
                role,
                http.Items.TryGetValue("CorrelationId", out var correlationId) ? correlationId?.ToString() : null,
                http.Connection.RemoteIpAddress?.ToString(),
                http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
        }
    }
}
