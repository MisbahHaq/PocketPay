using System.Security.Claims;
using Inventory.Application.Abstractions;
using Inventory.Domain.Identity;
using Microsoft.AspNetCore.Http;

namespace Inventory.Infrastructure.Services;

/// <summary>
/// Reads the acting user from the current HTTP request.
/// </summary>
/// <remarks>
/// <para>
/// Resolves the user id from <c>ClaimTypes.NameIdentifier</c> rather than parsing the
/// JWT again. A malformed id yields null instead of throwing, so a forged or
/// hand-crafted token cannot turn an unparseable claim into a 500.
/// </para>
/// <para>
/// Roles come from claims here (cheap, no I/O) whereas the <c>me</c> endpoint reads
/// them from the database (authoritative). That split is intentional: authorization
/// checks happen on every request and must not cost a query, and the 15-minute token
/// lifetime bounds how long a stale role can persist.
/// </para>
/// </remarks>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var principal = Principal;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : null;
        }
    }

    public string? Email =>
        Principal?.FindFirstValue("email")
        ?? Principal?.FindFirstValue(ClaimTypes.Email);

    public string? FullName =>
        Principal?.FindFirstValue(ClaimTypes.Name);

    public IReadOnlyCollection<string> Roles =>
        Principal is null ? [] : [.. Principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value)];

    public bool IsInRole(string role) =>
        !string.IsNullOrWhiteSpace(role) && (Principal?.IsInRole(role) ?? false);
}