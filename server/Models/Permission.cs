namespace server.Models
{
    public class Permission
    {
        public int PermissionId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
