using Microsoft.EntityFrameworkCore;
using server.Models;

namespace server.Data
{
    public partial class AppDbContext:DbContext
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

            // Inventory
            modelBuilder.Entity<server.Models.Inventory.StockBalance>(entity =>
            {
                entity.HasKey(sb => new { sb.WarehouseId, sb.ProductId });
                entity.Property(sb => sb.QuantityOnHand).HasColumnType("decimal(18,2)");
                entity.Property(sb => sb.ReservedQty).HasColumnType("decimal(18,2)");
                entity.HasOne(sb => sb.Warehouse).WithMany(w => w.StockBalances).HasForeignKey(sb => sb.WarehouseId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(sb => sb.Product).WithMany(p => p.StockBalances).HasForeignKey(sb => sb.ProductId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<server.Models.Inventory.Category>(entity =>
            {
                entity.HasKey(c => c.CategoryId);
                entity.HasOne(c => c.Organization).WithMany().HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<server.Models.Inventory.UnitOfMeasure>(entity =>
            {
                entity.HasKey(u => u.UnitId);
                entity.HasOne(u => u.Organization).WithMany().HasForeignKey(u => u.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<server.Models.Inventory.Supplier>(entity =>
            {
                entity.HasKey(s => s.SupplierId);
                entity.HasOne(s => s.Organization).WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<server.Models.Inventory.Customer>(entity =>
            {
                entity.HasKey(c => c.CustomerId);
                entity.HasOne(c => c.Organization).WithMany().HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<server.Models.Inventory.Warehouse>(entity =>
            {
                entity.HasKey(w => w.WarehouseId);
                entity.HasOne(w => w.Organization).WithMany().HasForeignKey(w => w.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<server.Models.Inventory.PurchaseOrder>(entity =>
            {
                entity.HasKey(p => p.POId);
                entity.Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");
                entity.HasOne(p => p.Organization).WithMany().HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.ApprovedBy).WithMany().HasForeignKey(p => p.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<server.Models.Inventory.GoodsReceiptNote>(entity =>
            {
                entity.HasKey(g => g.GRNId);
                entity.HasOne(g => g.Organization).WithMany().HasForeignKey(g => g.OrganizationId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(g => g.Supplier).WithMany().HasForeignKey(g => g.SupplierId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(g => g.Warehouse).WithMany().HasForeignKey(g => g.WarehouseId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(g => g.PO).WithMany().HasForeignKey(g => g.POId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(g => g.ReceivedBy).WithMany().HasForeignKey(g => g.ReceivedById).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<server.Models.Inventory.SalesOrder>(entity =>
            {
                entity.HasKey(s => s.SOId);
                entity.Property(s => s.TotalAmount).HasColumnType("decimal(18,2)");
                entity.HasOne(s => s.Organization).WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.Customer).WithMany().HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.Warehouse).WithMany().HasForeignKey(s => s.WarehouseId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(s => s.CreatedBy).WithMany().HasForeignKey(s => s.CreatedById).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<server.Models.Inventory.StockTransfer>(entity =>
            {
                entity.HasKey(t => t.TransferId);
                entity.HasOne(t => t.Organization).WithMany().HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.FromWarehouse).WithMany().HasForeignKey(t => t.FromWarehouseId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.ToWarehouse).WithMany().HasForeignKey(t => t.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<server.Models.Inventory.StockAdjustment>(entity =>
            {
                entity.HasKey(a => a.AdjustmentId);
                entity.HasOne(a => a.Organization).WithMany().HasForeignKey(a => a.OrganizationId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.Warehouse).WithMany().HasForeignKey(a => a.WarehouseId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.AdjustedBy).WithMany().HasForeignKey(a => a.AdjustedById).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<server.Models.Inventory.Product>(entity =>
            {
                entity.HasKey(p => p.ProductId);
                entity.HasIndex(p => new { p.OrganizationId, p.SKU }).IsUnique();
                entity.Property(p => p.CostPrice).HasColumnType("decimal(18,2)");
                entity.Property(p => p.SellingPrice).HasColumnType("decimal(18,2)");
                entity.HasOne(p => p.Organization).WithMany().HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.Unit).WithMany(u => u.Products).HasForeignKey(p => p.UnitId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<server.Models.Inventory.StockMovement>(entity =>
            {
                entity.HasKey(sm => sm.StockMovementId);
                entity.Property(sm => sm.Quantity).HasColumnType("decimal(18,2)");
                entity.Property(sm => sm.UnitCost).HasColumnType("decimal(18,2)");
                entity.HasOne(sm => sm.Organization).WithMany().HasForeignKey(sm => sm.OrganizationId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(sm => sm.Product).WithMany().HasForeignKey(sm => sm.ProductId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(sm => sm.Warehouse).WithMany().HasForeignKey(sm => sm.WarehouseId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(sm => sm.MovedBy).WithMany().HasForeignKey(sm => sm.MovedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<server.Models.Inventory.PurchaseOrderItem>(entity =>
            {
                entity.HasKey(i => i.POItemId);
                entity.Property(i => i.Quantity).HasColumnType("decimal(18,2)");
                entity.Property(i => i.ReceivedQty).HasColumnType("decimal(18,2)");
                entity.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(i => i.LineTotal).HasColumnType("decimal(18,2)");
                entity.HasOne(i => i.PO).WithMany(p => p.Items).HasForeignKey(i => i.POId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<server.Models.Inventory.GoodsReceiptItem>(entity =>
            {
                entity.HasKey(g => g.GRNItemId);
                entity.Property(i => i.QuantityReceived).HasColumnType("decimal(18,2)");
                entity.HasOne(i => i.GRN).WithMany(g => g.Items).HasForeignKey(i => i.GRNId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<server.Models.Inventory.SalesOrderItem>(entity =>
            {
                entity.HasKey(i => i.SOItemId);
                entity.Property(i => i.Quantity).HasColumnType("decimal(18,2)");
                entity.Property(i => i.FulfilledQty).HasColumnType("decimal(18,2)");
                entity.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(i => i.LineTotal).HasColumnType("decimal(18,2)");
                entity.HasOne(i => i.SO).WithMany(s => s.Items).HasForeignKey(i => i.SOId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<server.Models.Inventory.StockTransferItem>(entity =>
            {
                entity.HasKey(i => i.TransferItemId);
                entity.Property(i => i.Quantity).HasColumnType("decimal(18,2)");
                entity.HasOne(i => i.Transfer).WithMany(t => t.Items).HasForeignKey(i => i.TransferId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<server.Models.Inventory.StockAdjustmentItem>(entity =>
            {
                entity.HasKey(i => i.AdjustmentItemId);
                entity.Property(i => i.Quantity).HasColumnType("decimal(18,2)");
                entity.Property(i => i.OldQty).HasColumnType("decimal(18,2)");
                entity.Property(i => i.NewQty).HasColumnType("decimal(18,2)");
                entity.HasOne(i => i.Adjustment).WithMany(a => a.Items).HasForeignKey(i => i.AdjustmentId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            });

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
