namespace server.DTOs.ProjectDTO
{
    public class ProjectResponseDTO
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = String.Empty;

        public string Description { get; set; } = String.Empty;

        public string CreatorName { get; set; } = String.Empty;
    }
}
