namespace server.Models
{
    public class TaskItem
    {
        public int TaskItemId { get; set; }
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime DueDate { get; set; }

        public string Status { get; set; } = string.Empty;

        //Ownership
        public int CreatorId { get; set; }
        public User ? Creator { get; set; } 

        //Project Relation
        public int ProjectId { get; set; }
        public Project ? Project { get; set; } 

        //Task Assignment
        public ICollection<TaskAssignment> ? Assignments { get; set; }

        public ICollection<Comment>? Comments{ get; set; }
    }
}
