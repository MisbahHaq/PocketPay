namespace server.Models
{
    public class ProjectMember
    {
        public int UserId { get; set; }
        public User ? User { get; set; } 

        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        public string Role { get; set; } = string.Empty;

        public DateTime JoinedOn { get; set; } = DateTime.UtcNow;
    }
}
