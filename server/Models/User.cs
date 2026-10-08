namespace server.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;

        public string HashedPassword { get; set; } = string.Empty;

        //user can create multiple projects
        public ICollection<Project> ? Projects { get; set; }


        // ProjectMember Relation
        public ICollection<ProjectMember> ? ProjectMembers { get; set; } 

         //user can create multiple tasks
        public ICollection<TaskItem> ? TaskItems{ get; set; }


        // TaskAssignemtn Relation
        public ICollection<TaskAssignment> ? Assignments { get; set; } 

        //comments

        public ICollection<Comment> ? Comments { get; set; } 


        //userroles
        public ICollection<UserRole> ? UserRoles { get; set; }


    } 
}
