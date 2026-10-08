namespace server.DTOs.RoleDTO
{
    public class RoleResponseDTO
    {
        public int RoleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public List<string> Permissions { get; set; } = new();
        public int MemberCount { get; set; }
    }

    public class AssignableRoleDTO
    {
        public int RoleId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CreateRoleDTO
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<string> Permissions { get; set; } = new();
    }

    public class UpdateRoleDTO
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public List<string>? Permissions { get; set; }
    }

    public class PermissionCatalogDTO
    {
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
