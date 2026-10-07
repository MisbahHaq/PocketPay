namespace server.Models
{
    public class ProjectMember
    {
        public int UserId { get; set; }

        public User ? User { get; set; } = new User();

        public int ProjectId { get; set; }

        public Project Project { get; set; } = new Project();

        public string Role { get; set; } = string.Empty; // Dev, Admin, Owner

        public DateTime JoinedOn { get; set; } = new DateTime();
    }
}
