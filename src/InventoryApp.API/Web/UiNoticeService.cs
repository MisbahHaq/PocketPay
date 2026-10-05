using Microsoft.AspNetCore.DataProtection;

namespace InventoryApp.Web;

/// <summary>
/// Carries a one-shot message across the redirect that follows a form post.
/// </summary>
/// <remarks>
/// The auth screens are plain HTML form posts, which by nature end in a redirect so that a refresh
/// cannot resubmit the password. That leaves nowhere to render a failure, so the message rides back
/// on a cookie. The cookie is protected rather than plaintext: an attacker who can plant a cookie
/// in the victim's browser could otherwise render arbitrary text into the sign-in page.
/// </remarks>
public sealed class UiNoticeService(IDataProtectionProvider dataProtection)
{
    private const string CookieName = "InventoryApp.UiNotice";

    /// <summary>Purpose string. Bumping this invalidates notices issued by an older build.</summary>
    private const string Purpose = "InventoryApp.Web.UiNotice.v1";

    /// <summary>Long enough to survive a redirect, short enough that a stale message is never shown.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly IDataProtector _protector = dataProtection.CreateProtector(Purpose);

    /// <summary>Writes a notice for the next request to pick up.</summary>
    public void Set(HttpContext context, string message)
    {
        context.Response.Cookies.Append(
            CookieName,
            _protector.Protect(message),
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Strict,

                // The message is a rendering instruction for this browser only, so it is discarded
                // rather than persisted across sessions.
                Expires = DateTimeOffset.UtcNow.Add(Lifetime),
                MaxAge = Lifetime,
                Path = "/"
            });
    }

    /// <summary>
    /// Reads and clears the pending notice. Returns null when there is none, or when the cookie
    /// cannot be unprotected, which is treated as absent rather than surfaced as an error.
    /// </summary>
    public string? Take(HttpContext context)
    {
        var protectedMessage = context.Request.Cookies[CookieName];

        context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });

        if (string.IsNullOrEmpty(protectedMessage))
            return null;

        try
        {
            return _protector.Unprotect(protectedMessage);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}