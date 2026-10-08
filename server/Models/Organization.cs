namespace server.Models
{
    public class Organization
    {
        public int OrganizationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
        public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
        public ICollection<Project> Projects { get; set; } = new List<Project>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}
