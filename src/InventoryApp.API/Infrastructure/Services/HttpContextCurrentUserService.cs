using System.Security.Claims;
using InventoryApp.Application.Abstractions;

namespace InventoryApp.Infrastructure.Services;

/// <summary>
/// Reads the caller's identity from the validated access token. Registered per request because it
/// depends on IHttpContextAccessor.
/// </summary>
internal sealed class HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string? UserId =>
        Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? Principal?.FindFirst("sub")?.Value;

    public string? Email =>
        Principal?.FindFirst(ClaimTypes.Email)?.Value
        ?? Principal?.FindFirst("email")?.Value;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct().ToArray()
        ?? [];
}