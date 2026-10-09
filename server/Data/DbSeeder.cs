using Microsoft.EntityFrameworkCore;
using server.Helpers;
using server.Models;

namespace server.Data
{
    public static class DbSeeder
    {
        //demo logins, one per built-in role, used to showcase role-based access.
        //username, email, password, role name, short description of what the role can do
        private static readonly (string UserName, string Email, string Password, string Role)[] DemoAccounts = new[]
        {
            ("admin",    "admin@system.com",   "Admin@123",    RoleTemplates.OrgAdmin),
            ("manager",  "manager@demo.com",   "Manager@123",  RoleTemplates.Manager),
            ("employee", "employee@demo.com",  "Employee@123", RoleTemplates.Employee),
            ("viewer",   "viewer@demo.com",    "Viewer@123",   RoleTemplates.Client),
        };

        public static async Task SeedAsync(AppDbContext _dbContext, bool seedDemoData = false)
        {
            await _dbContext.Database.MigrateAsync();

            //1) permission catalog (global, shared by every organization)
            //   upsert so newly introduced permissions are picked up on existing databases
            var existingKeys = await _dbContext.Permissions.Select(p => p.Key).ToListAsync();
            foreach (var (key, description) in PermissionKeys.All)
            {
                if (!existingKeys.Contains(key))
                {
                    _dbContext.Permissions.Add(new Permission { Key = key, Description = description });
                }
            }
            await _dbContext.SaveChangesAsync();

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

            //4) sync built-in (system) roles to the current permission templates.
            //   Keeps Organization Admin on the full catalog and refreshes Manager/
            //   Employee/Client when the template evolves. Custom roles are untouched.
            var permissionsByKey = await _dbContext.Permissions
                .ToDictionaryAsync(p => p.Key, p => p.PermissionId);

            var systemRoles = await _dbContext.Roles
                .Where(r => r.IsSystem)
                .ToListAsync();

            foreach (var role in systemRoles)
            {
                List<string> desiredKeys;
                if (role.RoleName == RoleTemplates.OrgAdmin)
                {
                    desiredKeys = PermissionKeys.All.Select(p => p.Key).ToList();
                }
                else if (!RoleTemplates.Definitions.TryGetValue(role.RoleName, out desiredKeys!))
                {
                    continue;
                }

                var desiredIds = desiredKeys
                    .Where(permissionsByKey.ContainsKey)
                    .Select(k => permissionsByKey[k])
                    .ToHashSet();

                var current = await _dbContext.RolePermissions
                    .Where(rp => rp.RoleId == role.RoleId)
                    .ToListAsync();

                var currentIds = current.Select(rp => rp.PermissionId).ToHashSet();

                //remove permissions no longer part of the role
                foreach (var rp in current.Where(rp => !desiredIds.Contains(rp.PermissionId)))
                {
                    _dbContext.RolePermissions.Remove(rp);
                }

                //add newly required permissions
                foreach (var id in desiredIds.Where(id => !currentIds.Contains(id)))
                {
                    _dbContext.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.RoleId,
                        PermissionId = id,
                    });
                }
            }
            await _dbContext.SaveChangesAsync();

            //5) demo accounts (opt-in via "SeedDemoData"): one login per built-in role
            //   so each access level can be demonstrated. Idempotent - safe to run every start.
            if (seedDemoData)
            {
                var org = await _dbContext.Organizations
                    .OrderBy(o => o.OrganizationId)
                    .FirstOrDefaultAsync();

                if (org is not null)
                {
                    var rolesByName = await _dbContext.Roles
                        .Where(r => r.OrganizationId == org.OrganizationId)
                        .ToDictionaryAsync(r => r.RoleName, r => r.RoleId);

                    foreach (var demo in DemoAccounts)
                    {
                        var user = await _dbContext.Users
                            .FirstOrDefaultAsync(u => u.UserName == demo.UserName);

                        if (user is null)
                        {
                            user = new User
                            {
                                UserName = demo.UserName,
                                UserEmail = demo.Email,
                                HashedPassword = BCrypt.Net.BCrypt.HashPassword(demo.Password),
                                Status = UserStatus.Active,
                                OrganizationId = org.OrganizationId,
                            };
                            _dbContext.Users.Add(user);
                            await _dbContext.SaveChangesAsync();
                        }
                        else if (user.OrganizationId is null)
                        {
                            user.OrganizationId = org.OrganizationId;
                            user.Status = UserStatus.Active;
                            await _dbContext.SaveChangesAsync();
                        }

                        if (!rolesByName.TryGetValue(demo.Role, out var roleId)) continue;

                        var hasRole = await _dbContext.UserRoles
                            .AnyAsync(ur => ur.UserId == user.UserId && ur.RoleId == roleId);
                        if (!hasRole)
                        {
                            _dbContext.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = roleId });
                            await _dbContext.SaveChangesAsync();
                        }
                    }
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
