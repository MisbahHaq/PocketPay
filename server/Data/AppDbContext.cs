using Microsoft.EntityFrameworkCore;
using server.Models;

namespace server.Data
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions options):base(options)
        {
            
        }

        public DbSet<User> Users { get; set; }

        public DbSet<Project> Projects { get; set; }

        public DbSet<Comment> Comments { get; set; }

        public DbSet<ProjectMember> Members{ get; set; }

        public DbSet<TaskAssignment> Assignments{ get; set; }

        public DbSet<TaskItem> Tasks{ get; set; }

        public DbSet<Role> Roles { get; set; }

        public DbSet<UserRole> UserRoles { get; set; }

        public DbSet<Organization> Organizations { get; set; }

        public DbSet<Workspace> Workspaces { get; set; }

        public DbSet<Invitation> Invitations { get; set; }

        public DbSet<Permission> Permissions { get; set; }

        public DbSet<RolePermission> RolePermissions { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }

        //fluent api

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //user
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.UserId);

                entity.Property(u => u.UserName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(u => u.UserEmail)
                      .IsRequired()
                      .HasMaxLength(150);

                entity.Property(u => u.Status)
                      .IsRequired()
                      .HasMaxLength(30);

                entity.HasOne(u => u.Organization)
                      .WithMany(o => o.Users)
                      .HasForeignKey(u => u.OrganizationId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            //Organization
            modelBuilder.Entity<Organization>(entity =>
            {
                entity.HasKey(o => o.OrganizationId);

                entity.Property(o => o.Name)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(o => o.Description)
                      .HasMaxLength(1000);

                entity.HasOne(o => o.CreatedBy)
                      .WithMany()
                      .HasForeignKey(o => o.CreatedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            //Workspace
            modelBuilder.Entity<Workspace>(entity =>
            {
                entity.HasKey(w => w.WorkspaceId);

                entity.Property(w => w.Name)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.HasOne(w => w.Organization)
                      .WithMany(o => o.Workspaces)
                      .HasForeignKey(w => w.OrganizationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            //Invitation
            modelBuilder.Entity<Invitation>(entity =>
            {
                entity.HasKey(i => i.InvitationId);

                entity.Property(i => i.Email)
                      .IsRequired()
                      .HasMaxLength(300);

                entity.Property(i => i.Token)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.HasIndex(i => i.Token);

                entity.Property(i => i.Status)
                      .IsRequired()
                      .HasMaxLength(30);

                entity.HasOne(i => i.Organization)
                      .WithMany(o => o.Invitations)
                      .HasForeignKey(i => i.OrganizationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.Role)
                      .WithMany()
                      .HasForeignKey(i => i.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.InvitedBy)
                      .WithMany()
                      .HasForeignKey(i => i.InvitedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            //Permission
            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasKey(p => p.PermissionId);

                entity.Property(p => p.Key)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.HasIndex(p => p.Key).IsUnique();

                entity.Property(p => p.Description)
                      .HasMaxLength(500);
            });

            //RolePermission
            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

                entity.HasOne(rp => rp.Role)
                      .WithMany(r => r.RolePermissions)
                      .HasForeignKey(rp => rp.RoleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rp => rp.Permission)
                      .WithMany(p => p.RolePermissions)
                      .HasForeignKey(rp => rp.PermissionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            //AuditLog
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(a => a.AuditLogId);

                entity.Property(a => a.Action)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(a => a.TargetType)
                      .HasMaxLength(100);

                entity.Property(a => a.TargetId)
                      .HasMaxLength(100);

                entity.HasOne(a => a.Organization)
                      .WithMany(o => o.AuditLogs)
                      .HasForeignKey(a => a.OrganizationId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Actor)
                      .WithMany()
                      .HasForeignKey(a => a.ActorUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            //Project
            modelBuilder.Entity<Project>(entity =>
            {
                entity.HasKey(p => p.ProjectId);

                entity.Property(p => p.ProjectName)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(p => p.ProjectDescription)
                      .HasMaxLength(1000);

                entity.Property(p => p.CreatedAt)
                      .HasColumnType("datetime2")
                      .HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(p => p.Organization)
                      .WithMany(o => o.Projects)
                      .HasForeignKey(p => p.OrganizationId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Creator relationship (User -> Projects)
                entity.HasOne(p => p.Creator)
                      .WithMany(u => u.Projects)
                      .HasForeignKey(p => p.CreatorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            //TaskItem
            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.HasKey(t => t.TaskItemId);

                entity.Property(t => t.Title)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(t => t.Status)
                      .HasMaxLength(50);

                // Relationship (Project-TaskItems)
                entity.HasOne(t => t.Project)
                      .WithMany(p => p.TaskItems)
                      .HasForeignKey(t => t.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Creator relation (User -> Tasks)
                entity.HasOne(t => t.Creator)
                      .WithMany(u => u.TaskItems)
                      .HasForeignKey(t => t.CreatorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });


            //comment
            modelBuilder.Entity<Comment>(entity =>
            {
                entity.HasKey(c => c.CommentId);

                entity.Property(c => c.Content)
                      .IsRequired()
                      .HasMaxLength(1000);

                // Relationship (Comment - Task)
                entity.HasOne(c => c.TaskItem)
                      .WithMany(t => t.Comments)
                      .HasForeignKey(c => c.TaskId)
                      .OnDelete(DeleteBehavior.Cascade);

                // User relationship
                entity.HasOne(c => c.User)
                      .WithMany(u => u.Comments)
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });


            //project member
            modelBuilder.Entity<ProjectMember>(entity =>
            {
                entity.HasKey(pm => new { pm.UserId, pm.ProjectId });

                entity.Property(pm => pm.Role)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.HasOne(pm => pm.User)
                      .WithMany(u => u.ProjectMembers)
                      .HasForeignKey(pm => pm.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pm => pm.Project)
                      .WithMany(p => p.ProjectMembers)
                      .HasForeignKey(pm => pm.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            //Task Assignment
            modelBuilder.Entity<TaskAssignment>(entity =>
            {
                entity.HasKey(ta => new { ta.UserId, ta.TaskItemId });

                entity.HasOne(ta => ta.User)
                      .WithMany(u => u.Assignments)
                      .HasForeignKey(ta => ta.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ta => ta.TaskItem)
                      .WithMany(t => t.Assignments)
                      .HasForeignKey(ta => ta.TaskItemId)
                      .OnDelete(DeleteBehavior.Cascade);
            });


            //Role
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(r => r.RoleId);

                entity.Property(r => r.RoleName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(r => r.Description)
                      .HasMaxLength(500);

                entity.HasOne(r => r.Organization)
                      .WithMany()
                      .HasForeignKey(r => r.OrganizationId)
                      .OnDelete(DeleteBehavior.Restrict);
            });


            //UserRole
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.HasKey(ur => new { ur.UserId, ur.RoleId });

                entity.HasOne(ur => ur.User)
                      .WithMany(u => u.UserRoles)
                      .HasForeignKey(ur => ur.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ur => ur.Role)
                      .WithMany(r => r.UserRoles)
                      .HasForeignKey(ur => ur.RoleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }



    }
}
