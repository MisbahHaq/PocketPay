namespace server.Models
{
    public static class UserStatus
    {
        public const string Invited = "Invited";
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Suspended = "Suspended";
        public const string Deactivated = "Deactivated";
        public const string Removed = "Removed";
    }

    public class User
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;

        public string HashedPassword { get; set; } = string.Empty;

        //account state: Invited | Pending | Active | Suspended | Deactivated | Removed
        public string Status { get; set; } = UserStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        //tenant membership (null = not part of any organization yet)
        public int? OrganizationId { get; set; }
        public Organization? Organization { get; set; }

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
