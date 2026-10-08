using Microsoft.EntityFrameworkCore;
using server.Helpers;
using server.Models;

namespace server.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext _dbContext)
        {
            await _dbContext.Database.MigrateAsync();

            //1) permission catalog (global, shared by every organization)
            if (!await _dbContext.Permissions.AnyAsync())
            {
                foreach (var (key, description) in PermissionKeys.All)
                {
                    _dbContext.Permissions.Add(new Permission { Key = key, Description = description });
                }
                await _dbContext.SaveChangesAsync();
            }

            //2) development admin account (only when the database has no users at all)
            if (!await _dbContext.Users.AnyAsync())
            {
                _dbContext.Users.Add(new User
                {
                    UserName = "Admin",
                    UserEmail = "admin@system.com",
                    HashedPassword = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    Status = UserStatus.Active,
                });
                await _dbContext.SaveChangesAsync();
            }
            else
            {
                var admin = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserName == "Admin");
                if (admin is not null && string.IsNullOrWhiteSpace(admin.HashedPassword))
                {
                    admin.HashedPassword = BCrypt.Net.BCrypt.HashPassword("Admin@123");
                    await _dbContext.SaveChangesAsync();
                }
            }

            //3) default organization - created once, adopts every account that existed before
            //   organizations were introduced (so no legacy data is lost)
            if (!await _dbContext.Organizations.AnyAsync())
            {
                var creator = await _dbContext.Users.OrderBy(u => u.UserId).FirstOrDefaultAsync();
                if (creator is not null)
                {
                    var organization = new Organization
                    {
                        Name = "Default Organization",
                        Description = "Automatically created for existing accounts.",
                        CreatedById = creator.UserId,
                        CreatedAt = DateTime.UtcNow,
                    };
                    _dbContext.Organizations.Add(organization);
                    _dbContext.Workspaces.Add(new Workspace
                    {
                        Organization = organization,
                        Name = "Main Workspace",
                    });
                    await _dbContext.SaveChangesAsync();

                    //default roles + permission matrix
                    var permissions = await _dbContext.Permissions.ToDictionaryAsync(p => p.Key);
                    foreach (var roleName in RoleTemplates.DefaultRoleNames)
                    {
                        var role = new Role
                        {
                            OrganizationId = organization.OrganizationId,
                            RoleName = roleName,
                            Description = RoleTemplates.Descriptions[roleName],
                            IsSystem = true,
                        };
                        _dbContext.Roles.Add(role);
                        await _dbContext.SaveChangesAsync();

                        foreach (var key in RoleTemplates.Definitions[roleName])
                        {
                            if (!permissions.TryGetValue(key, out var permission)) continue;
                            _dbContext.RolePermissions.Add(new RolePermission
                            {
                                RoleId = role.RoleId,
                                PermissionId = permission.PermissionId,
                            });
                        }
                    }
                    await _dbContext.SaveChangesAsync();

                    var adminRole = await _dbContext.Roles.FirstAsync(r =>
                        r.OrganizationId == organization.OrganizationId &&
                        r.RoleName == RoleTemplates.OrgAdmin);

                    //adopt every org-less user, mapping the old single-role schema
                    var users = await _dbContext.Users
                        .Where(u => u.OrganizationId == null)
                        .ToListAsync();

                    foreach (var user in users)
                    {
                        user.OrganizationId = organization.OrganizationId;
                        user.Status = UserStatus.Active;

                        var targetName = user.UserId == creator.UserId
                            ? RoleTemplates.OrgAdmin
                            : await MapLegacyRole(user.UserId) ?? RoleTemplates.Employee;

                        var targetRole = await _dbContext.Roles.FirstAsync(r =>
                            r.OrganizationId == organization.OrganizationId && r.RoleName == targetName);

                        _dbContext.UserRoles.Add(new UserRole
                        {
                            UserId = user.UserId,
                            RoleId = targetRole.RoleId,
                        });
                    }

                    //drop the obsolete global roles (their memberships were migrated above)
                    var legacyRoles = await _dbContext.Roles
                        .Where(r => r.OrganizationId == null)
                        .ToListAsync();
                    _dbContext.Roles.RemoveRange(legacyRoles);

                    await _dbContext.SaveChangesAsync();
                }
            }

            async Task<string?> MapLegacyRole(int userId)
            {
                var names = await _dbContext.UserRoles.AsNoTracking()
                    .Where(ur => ur.UserId == userId)
                    .Select(ur => ur.Role!.RoleName)
                    .ToListAsync();

                foreach (var name in names)
                {
                    if (RoleTemplates.LegacyMapping.TryGetValue(name, out var mapped))
                        return mapped;
                }
                return null;
            }
        }
    }
}
