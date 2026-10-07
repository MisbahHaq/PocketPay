namespace server.Models
{
    public class Project
    {
        public int ProjectId { get; set; }

        public string ProjectName { get; set; } = string.Empty;

        public string ProjectDescription { get; set; } = string.Empty;

        // Ownership
        public int CreatorId { get; set; }
        public User Creator { get; set; } = new User();


        // Task

        public ICollection<TaskItem> TaskItems { get; set; } = new 
            List<TaskItem>();

        // Project Member Relation

        public ICollection<ProjectMember> ProjectMembers { get; set; } = new
            List<ProjectMember>();
    }
}
