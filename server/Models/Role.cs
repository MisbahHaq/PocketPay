namespace server.Models
{
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        //null = legacy/system role, otherwise scoped to a single organization
        public int? OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        //built-in roles (Organization Admin / Manager / Employee / Client) cannot be deleted or renamed
        public bool IsSystem { get; set; }

        public ICollection<RolePermission>? RolePermissions { get; set; }

        //userroles
        public ICollection<UserRole>? UserRoles { get; set; }
    }
}
