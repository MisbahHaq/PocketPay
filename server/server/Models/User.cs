namespace server.Models
{
    public class User
    {
        public int UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string UserEmail { get; set; } = string.Empty;

        // User can have many projects

        public ICollection<Project> Projects { get; set; } = new 
            List<Project>();

        // User can create multiple tasks

        public ICollection<TaskItem> TaskItems { get; set; } = new
            List<TaskItem>();

        // Task Assignment Relation

        public ICollection<TaskAssignment> Assignments { get; set; } = new
            List<TaskAssignment>();

        // Comments

        public ICollection<Comment> Comments { get; set; } = new
            List<Comment>();
    }
}
