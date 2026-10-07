namespace server.Models
{
    public class User
    {
        public int UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string UserEmail { get; set; } = string.Empty;

        // User can have many projects

        public ICollection<Project> Projects { get; set; } = new List<Project>();
    }
}
