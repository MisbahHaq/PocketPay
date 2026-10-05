namespace InventoryApp.Domain.Constants;

/// <summary>
/// The three roles seeded on startup. Stored as constants so a typo cannot
/// produce a role that nothing maps to.
/// </summary>
public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    public static IReadOnlyList<string> All { get; } = [Admin, Manager, Staff];
}
