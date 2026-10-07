using Microsoft.EntityFrameworkCore;
namespace server.Models
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions options): base(options)
        {
            
        }
        public DbSet<User> Users { get; set; }

        public DbSet<Project> Projects { get; set; }

        public DbSet<Comment> Comments { get; set; }

        public DbSet<ProjectMember> Members { get; set; }

        public DbSet<TaskAssignment> Assignments { get; set; }

        public DbSet<TaskItem> Tasks { get; set; }

    }
}
