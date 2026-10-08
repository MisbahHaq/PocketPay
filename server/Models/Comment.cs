namespace server.Models
{
    public class Comment
    {
        public int CommentId { get; set; }
        public string Content { get; set; } = string.Empty;

        public DateTime Created { get; set; }

        // Task Relationship
        public int TaskId { get; set; }
        public TaskItem ? TaskItem { get; set; } 

        //user relationship
        public int UserId { get; set; }
        public User ? User { get; set; } 

    }
}
