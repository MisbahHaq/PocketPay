namespace server.Models
{
    public class Project
    {
        public int ProjectId { get; set; } 
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectDescription { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        //tenant (null only for legacy rows before backfill)
        public int? OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        // Ownership
        public int CreatorId { get; set; }
        public User ? Creator { get; set; } 

        //tasks
        public ICollection<TaskItem> ? TaskItems { get; set; } 

        // ProjectMember Relation
        public ICollection<ProjectMember> ? ProjectMembers { get; set; } 

    }
}
