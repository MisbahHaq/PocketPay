namespace InventoryApp.Web;

/// <summary>
/// Authentication scheme names for the browser UI.
/// </summary>
/// <remarks>
/// The API is a pure bearer-token surface with no cookie, which is the right shape for machine
/// clients. A Blazor Server UI cannot be: the browser holds no token, and an interactive circuit
/// cannot attach one to a component render. So the UI signs in with a cookie instead.
/// </remarks>
public static class AuthSchemes
{
    /// <summary>Cookie handler backing the UI session.</summary>
    public const string UiCookie = "InventoryApp.UiCookie";

    /// <summary>
    /// Default scheme. Picks the handler per request: JWT for /api, cookie for everything else.
    /// </summary>
    public const string Selector = "InventoryApp.Selector";
}