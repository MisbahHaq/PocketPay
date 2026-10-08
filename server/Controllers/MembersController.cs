using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Authorization;
using server.Data;
using server.DTOs.MemberDTO;
using server.Helpers;
using server.Models;
using server.Services;

namespace server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MembersController : ControllerBase
    {
        private static readonly HashSet<string> AllowedStatuses = new()
        {
            UserStatus.Active, UserStatus.Suspended, UserStatus.Deactivated
        };

        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;

        public MembersController(AppDbContext db, ICurrentUser current, IAuditLogService audit)
        {
            _db = db;
            _current = current;
            _audit = audit;
        }

        [Permission(PermissionKeys.ManageUsers)]
        [HttpGet]
        public async Task<ActionResult> List()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;

            var members = await _db.Users.AsNoTracking()
                .Where(u => u.OrganizationId == orgId && u.Status != UserStatus.Removed)
                .OrderBy(u => u.UserName)
                .Select(u => new MemberResponseDTO
                {
                    UserId = u.UserId,
                    UserName = u.UserName,
                    UserEmail = u.UserEmail,
                    Status = u.Status,
                    Roles = u.UserRoles!
                        .Where(ur => ur.Role!.OrganizationId == orgId)
                        .Select(ur => ur.Role!.RoleName)
                        .ToList(),
                    CreatedAt = u.CreatedAt,
                })
                .ToListAsync();

            return Ok(members);
        }

        //minimal directory used to assign people to projects
        [Permission(PermissionKeys.EditProject)]
        [HttpGet("assignable")]
        public async Task<ActionResult> ListAssignable()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;

            var members = await _db.Users.AsNoTracking()
                .Where(u => u.OrganizationId == orgId && u.Status == UserStatus.Active)
                .OrderBy(u => u.UserName)
                .Select(u => new AssignableMemberDTO
                {
                    UserId = u.UserId,
                    UserName = u.UserName,
                    Status = u.Status,
                    RoleName = u.UserRoles!
                        .Where(ur => ur.Role!.OrganizationId == orgId)
                        .Select(ur => ur.Role!.RoleName)
                        .FirstOrDefault() ?? "",
                })
                .ToListAsync();

            return Ok(members);
        }

        //change role and/or account status
        [Permission(PermissionKeys.ManageUsers)]
        [HttpPatch("{id:int}")]
        public async Task<ActionResult> Update(int id, UpdateMemberDTO dto)
        {
            var actor = await _current.RequireOrgUserAsync();
            var orgId = actor.OrganizationId!.Value;

            if (id == actor.UserId)
                return BadRequest("You cannot change your own role or status.");

            var member = await _db.Users
                .FirstOrDefaultAsync(u => u.UserId == id && u.OrganizationId == orgId);
            if (member is null) return NotFound();

            var changed = new Dictionary<string, object?>();

            if (dto.RoleId is not null)
            {
                var newRole = await _db.Roles.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.RoleId == dto.RoleId && r.OrganizationId == orgId);
                if (newRole is null) return BadRequest("Role does not exist in this organization.");

                //nobody can grant permissions they do not hold themselves
                var granted = await _db.RolePermissions.AsNoTracking()
                    .Where(rp => rp.RoleId == newRole.RoleId)
                    .Select(rp => rp.Permission!.Key)
                    .ToListAsync();
                if (!granted.All(_current.Can))
                    return StatusCode(StatusCodes.Status403Forbidden,
                        "You cannot assign a role with permissions you do not have.");

                var currentRoles = await _db.UserRoles
                    .Where(ur => ur.UserId == member.UserId)
                    .ToListAsync();

                var orgRoleIds = await _db.Roles.AsNoTracking()
                    .Where(r => r.OrganizationId == orgId)
                    .Select(r => r.RoleId)
                    .ToListAsync();

                foreach (var ur in currentRoles.Where(ur => orgRoleIds.Contains(ur.RoleId)))
                {
                    _db.UserRoles.Remove(ur);
                }
                _db.UserRoles.Add(new UserRole { UserId = member.UserId, RoleId = newRole.RoleId });

                changed["role"] = newRole.RoleName;
            }

            if (!string.IsNullOrWhiteSpace(dto.Status))
            {
                if (!AllowedStatuses.Contains(dto.Status))
                    return BadRequest("Status must be Active, Suspended or Deactivated.");

                member.Status = dto.Status;
                changed["status"] = dto.Status;
            }

            if (changed.Count == 0)
                return BadRequest("Nothing to update.");

            await _db.SaveChangesAsync();

            if (changed.ContainsKey("role"))
                await _audit.WriteAsync("member.role.changed", "User", member.UserId.ToString(),
                    new { member.UserName, Role = changed["role"] });
            if (changed.ContainsKey("status"))
                await _audit.WriteAsync("member.status.changed", "User", member.UserId.ToString(),
                    new { member.UserName, status = member.Status });

            return Ok(new { message = "Member updated.", changed });
        }

        //remove from the organization; the account and its history survive
        [Permission(PermissionKeys.RemoveUsers)]
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Remove(int id)
        {
            var actor = await _current.RequireOrgUserAsync();
            var orgId = actor.OrganizationId!.Value;

            if (id == actor.UserId)
                return BadRequest("You cannot remove yourself.");

            var member = await _db.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.UserId == id && u.OrganizationId == orgId);
            if (member is null) return NotFound();

            member.Status = UserStatus.Removed;
            member.OrganizationId = null;
            if (member.UserRoles is not null)
            {
                foreach (var role in member.UserRoles.ToList())
                {
                    _db.UserRoles.Remove(role);
                }
            }

            await _db.SaveChangesAsync();

            await _audit.WriteAsync("member.removed", "User", member.UserId.ToString(),
                new { member.UserName, member.UserEmail });

            return Ok(new { message = "Member removed. Their history is preserved." });
        }
    }
}
