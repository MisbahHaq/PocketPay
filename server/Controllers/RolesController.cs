using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Authorization;
using server.Data;
using server.DTOs.RoleDTO;
using server.Helpers;
using server.Models;
using server.Services;

namespace server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;

        public RolesController(AppDbContext db, ICurrentUser current, IAuditLogService audit)
        {
            _db = db;
            _current = current;
            _audit = audit;
        }

        //role management screen
        [Permission(PermissionKeys.ManageRoles)]
        [HttpGet]
        public async Task<ActionResult> List()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;

            var roles = await _db.Roles.AsNoTracking()
                .Where(r => r.OrganizationId == orgId)
                .OrderBy(r => r.IsSystem ? 0 : 1).ThenBy(r => r.RoleName)
                .Select(r => new RoleResponseDTO
                {
                    RoleId = r.RoleId,
                    Name = r.RoleName,
                    Description = r.Description,
                    IsSystem = r.IsSystem,
                    Permissions = r.RolePermissions!.Select(rp => rp.Permission!.Key).ToList(),
                    MemberCount = r.UserRoles!.Count(),
                })
                .ToListAsync();

            return Ok(roles);
        }

        //lightweight list for invitation / member forms
        [Permission(PermissionKeys.InviteUsers)]
        [HttpGet("assignable")]
        public async Task<ActionResult> ListAssignable()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;

            var roles = await _db.Roles.AsNoTracking()
                .Where(r => r.OrganizationId == orgId)
                .OrderBy(r => r.IsSystem ? 0 : 1).ThenBy(r => r.RoleName)
                .Select(r => new AssignableRoleDTO { RoleId = r.RoleId, Name = r.RoleName })
                .ToListAsync();

            return Ok(roles);
        }

        [Permission(PermissionKeys.ManageRoles)]
        [HttpPost]
        public async Task<ActionResult> Create(CreateRoleDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;

            var name = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Role name is required.");
            if (await _db.Roles.AnyAsync(r => r.OrganizationId == orgId && r.RoleName == name))
                return BadRequest("A role with that name already exists.");

            var keys = dto.Permissions.Distinct().ToList();
            var valid = await _db.Permissions.AsNoTracking().Select(p => p.Key).ToListAsync();
            var unknown = keys.Except(valid).ToList();
            if (unknown.Count > 0) return BadRequest($"Unknown permissions: {string.Join(", ", unknown)}");

            var role = new Role
            {
                OrganizationId = orgId,
                RoleName = name,
                Description = dto.Description?.Trim() ?? "",
                IsSystem = false,
            };
            _db.Roles.Add(role);
            await _db.SaveChangesAsync();

            await AddPermissionsAsync(role.RoleId, keys);
            await _db.SaveChangesAsync();

            await _audit.WriteAsync("role.created", "Role", role.RoleId.ToString(),
                new { name, permissions = keys });

            return Ok(new { roleId = role.RoleId, name });
        }

        //rename (custom roles) and/or replace the permission set
        [Permission(PermissionKeys.ManageRoles)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, UpdateRoleDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var role = await _db.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.RoleId == id && r.OrganizationId == orgId);
            if (role is null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.Name) && !role.IsSystem)
            {
                var name = dto.Name.Trim();
                if (await _db.Roles.AnyAsync(r =>
                        r.OrganizationId == orgId && r.RoleId != id && r.RoleName == name))
                    return BadRequest("A role with that name already exists.");
                role.RoleName = name;
            }

            if (dto.Description is not null) role.Description = dto.Description.Trim();

            if (dto.Permissions is not null)
            {
                var keys = dto.Permissions.Distinct().ToList();
                var valid = await _db.Permissions.AsNoTracking().Select(p => p.Key).ToListAsync();
                var unknown = keys.Except(valid).ToList();
                if (unknown.Count > 0) return BadRequest($"Unknown permissions: {string.Join(", ", unknown)}");

                if (role.RolePermissions is not null)
                {
                    foreach (var existing in role.RolePermissions.ToList())
                    {
                        _db.RolePermissions.Remove(existing);
                    }
                }
                await AddPermissionsAsync(role.RoleId, keys);
                await _db.SaveChangesAsync();

                await _audit.WriteAsync("role.permissions.updated", "Role", role.RoleId.ToString(),
                    new { role.RoleName, permissions = keys });
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Role updated." });
        }

        [Permission(PermissionKeys.ManageRoles)]
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var role = await _db.Roles
                .FirstOrDefaultAsync(r => r.RoleId == id && r.OrganizationId == orgId);
            if (role is null) return NotFound();

            if (role.IsSystem) return BadRequest("Built-in roles cannot be deleted.");
            if (await _db.UserRoles.AnyAsync(ur => ur.RoleId == id))
                return BadRequest("Reassign the members using this role first.");

            _db.Roles.Remove(role);
            await _db.SaveChangesAsync();

            await _audit.WriteAsync("role.deleted", "Role", id.ToString(), new { role.RoleName });
            return Ok(new { message = "Role deleted." });
        }

        private async Task AddPermissionsAsync(int roleId, List<string> keys)
        {
            var permissions = await _db.Permissions.AsNoTracking()
                .Where(p => keys.Contains(p.Key))
                .ToListAsync();

            foreach (var permission in permissions)
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permission.PermissionId,
                });
            }
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class PermissionsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public PermissionsController(AppDbContext db) => _db = db;

        [Permission(PermissionKeys.ManageRoles)]
        [HttpGet]
        public async Task<ActionResult> List()
        {
            var activeKeys = PermissionKeys.All.Select(p => p.Key).ToList();
            var permissions = await _db.Permissions.AsNoTracking()
                .Where(p => activeKeys.Contains(p.Key))
                .OrderBy(p => p.Key)
                .Select(p => new PermissionCatalogDTO { Key = p.Key, Description = p.Description })
                .ToListAsync();
            return Ok(permissions);
        }
    }
}
