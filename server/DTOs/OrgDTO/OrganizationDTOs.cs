namespace server.DTOs.OrgDTO
{
    public class CreateOrganizationDTO
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? WorkspaceName { get; set; }
    }

    public class UpdateOrganizationDTO
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? WorkspaceName { get; set; }
    }

    public class WorkspaceResponseDTO
    {
        public int WorkspaceId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class OrganizationResponseDTO
    {
        public int OrganizationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public WorkspaceResponseDTO? Workspace { get; set; }
        public int MemberCount { get; set; }
        public int ProjectCount { get; set; }
    }
}
