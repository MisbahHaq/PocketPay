using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Authorization;
using server.Data;
using server.DTOs.OrgDTO;
using server.Helpers;
using server.Models;
using server.Services;

namespace server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrganizationsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;

        public OrganizationsController(AppDbContext db, ICurrentUser current, IAuditLogService audit)
        {
            _db = db;
            _current = current;
            _audit = audit;
        }

        //create an organization - the creator becomes the Organization Admin
        [Authorize]
        [HttpPost]
        public async Task<ActionResult> CreateOrganization(CreateOrganizationDTO dto)
        {
            var user = await _current.LoadAsync();
            if (user is null) return Unauthorized();
            if (user.OrganizationId is not null)
                return BadRequest("You already belong to an organization.");

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Organization name is required.");

            var trackedUser = await _db.Users.FirstAsync(u => u.UserId == user.UserId);

            var organization = new Organization
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                CreatedById = user.UserId,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Organizations.Add(organization);

            _db.Workspaces.Add(new Workspace
            {
                Organization = organization,
                Name = string.IsNullOrWhiteSpace(dto.WorkspaceName) ? dto.Name.Trim() : dto.WorkspaceName!.Trim(),
            });

            await _db.SaveChangesAsync();

            //seed the default role set with its default permission matrix
            var permissions = await _db.Permissions.ToDictionaryAsync(p => p.Key);
            foreach (var roleName in RoleTemplates.DefaultRoleNames)
            {
                var role = new Role
                {
                    OrganizationId = organization.OrganizationId,
                    RoleName = roleName,
                    Description = RoleTemplates.Descriptions[roleName],
                    IsSystem = true,
                };
                _db.Roles.Add(role);
                await _db.SaveChangesAsync();

                foreach (var key in RoleTemplates.Definitions[roleName])
                {
                    if (!permissions.TryGetValue(key, out var permission)) continue;
                    _db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = permission.PermissionId });
                }
            }
            await _db.SaveChangesAsync();

            //creator joins as Organization Admin
            var adminRole = await _db.Roles.FirstAsync(r =>
                r.OrganizationId == organization.OrganizationId && r.RoleName == RoleTemplates.OrgAdmin);

            trackedUser.OrganizationId = organization.OrganizationId;
            trackedUser.Status = UserStatus.Active;
            _db.UserRoles.Add(new UserRole { UserId = trackedUser.UserId, RoleId = adminRole.RoleId });

            await _db.SaveChangesAsync();

            await _audit.WriteForAsync(organization.OrganizationId, user.UserId, "organization.created", "Organization",
                organization.OrganizationId.ToString(), new { organization.Name, workspace = dto.WorkspaceName });

            return Ok(await GetOrganizationDtoAsync(organization.OrganizationId));
        }

        [OrgMember]
        [HttpGet("current")]
        public async Task<ActionResult> GetCurrent()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            return Ok(await GetOrganizationDtoAsync(orgId));
        }

        [Permission(PermissionKeys.ManageOrganization)]
        [HttpPatch("current")]
        public async Task<ActionResult> UpdateCurrent(UpdateOrganizationDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();
            var organization = await _db.Organizations.FirstAsync(o => o.OrganizationId == user.OrganizationId);

            if (!string.IsNullOrWhiteSpace(dto.Name)) organization.Name = dto.Name.Trim();
            if (dto.Description is not null) organization.Description = dto.Description.Trim();

            if (!string.IsNullOrWhiteSpace(dto.WorkspaceName))
            {
                var workspace = await _db.Workspaces.FirstAsync(w => w.OrganizationId == organization.OrganizationId);
                workspace.Name = dto.WorkspaceName.Trim();
            }

            await _db.SaveChangesAsync();
            await _audit.WriteAsync("organization.updated", "Organization",
                organization.OrganizationId.ToString(), new { dto.Name, dto.Description, dto.WorkspaceName });

            return Ok(await GetOrganizationDtoAsync(organization.OrganizationId));
        }

        private async Task<OrganizationResponseDTO> GetOrganizationDtoAsync(int organizationId)
        {
            var organization = await _db.Organizations.AsNoTracking()
                .Where(o => o.OrganizationId == organizationId)
                .Select(o => new OrganizationResponseDTO
                {
                    OrganizationId = o.OrganizationId,
                    Name = o.Name,
                    Description = o.Description,
                    CreatedAt = o.CreatedAt,
                    MemberCount = o.Users.Count(u => u.Status != UserStatus.Removed),
                    ProjectCount = o.Projects.Count(),
                })
                .FirstAsync();

            var workspace = await _db.Workspaces.AsNoTracking()
                .Where(w => w.OrganizationId == organizationId)
                .OrderBy(w => w.WorkspaceId)
                .Select(w => new WorkspaceResponseDTO { WorkspaceId = w.WorkspaceId, Name = w.Name })
                .FirstOrDefaultAsync();

            organization.Workspace = workspace;
            return organization;
        }
    }
}
