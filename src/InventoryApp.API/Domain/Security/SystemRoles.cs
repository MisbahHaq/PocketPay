namespace InventoryApp.Domain.Security;

/// <summary>
/// Role names seeded at startup. Constants rather than an enum because ASP.NET Core Identity
/// persists and compares role names as strings, so an enum would only hide stringly-typed bugs.
/// </summary>
public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = [Admin, Manager, Staff];
}
