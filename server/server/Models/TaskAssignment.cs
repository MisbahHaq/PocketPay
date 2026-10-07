namespace server.Models
{
    public class TaskAssignment
    {
        public int UserId { get; set; }

        public User User { get; set; } = new User();

        public int TaskItemId { get; set; }

        public TaskItem TaskItem { get; set; } = new TaskItem();

        public DateTime JoinedOn { get; set; } = new DateTime();
    }
}
