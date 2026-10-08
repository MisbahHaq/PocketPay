namespace server.DTOs.ProjectDTO
{
    public class AddProjectMemberDTO
    {
        public int UserId { get; set; }
        public string? Role { get; set; }
    }

    public class ProjectMemberResponseDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime JoinedOn { get; set; }
    }
}
