using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Authorization;
using server.Data;
using server.DTOs.ProjectDTO;
using server.Helpers;
using server.Models;
using server.Services;

namespace server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;

        public ProjectsController(AppDbContext db, ICurrentUser current, IAuditLogService audit)
        {
            _dbContext = db;
            _current = current;
            _audit = audit;
        }

        //create project
        [HttpPost("create")]
        [Permission(PermissionKeys.ManageProducts)]
        public async Task<ActionResult> CreateProject(CreateProjectDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();

            var project = new Project
            {
                ProjectName = dto.ProjectName,
                ProjectDescription = dto.Description,
                CreatorId = user.UserId,
                OrganizationId = user.OrganizationId,
            };

            await _dbContext.Projects.AddAsync(project);
            await _dbContext.SaveChangesAsync();

            await _audit.WriteAsync("project.created", "Project", project.ProjectId.ToString(),
                new { project.ProjectName });

            return Ok(new { message = "Project Create", project.ProjectId });
        }

        //update project
        [HttpPut("{id:int}")]
        [Permission(PermissionKeys.ManageProducts)]
        public async Task<ActionResult> UpdateProjectById(int id,CreateProjectDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();

            var project = await _dbContext.Projects
                .FirstOrDefaultAsync(p => p.ProjectId == id && p.OrganizationId == user.OrganizationId);

            if (project == null)
            {
                return NotFound();
            }

            project.ProjectName = dto.ProjectName;
            project.ProjectDescription = dto.Description;
            await _dbContext.SaveChangesAsync();

            await _audit.WriteAsync("project.updated", "Project", project.ProjectId.ToString(),
                new { project.ProjectName });

            return Ok(project);
        }

        //delete project
        [HttpDelete("{id:int}")]
        [Permission(PermissionKeys.ManageProducts)]
        public async Task<ActionResult> DeleteProjectById(int id)
        {
            var user = await _current.RequireOrgUserAsync();

            var project = await _dbContext.Projects
                .FirstOrDefaultAsync(p => p.ProjectId == id && p.OrganizationId == user.OrganizationId);

            if (project == null)
            {
                return NotFound();
            }

            _dbContext.Projects.Remove(project);
            await _dbContext.SaveChangesAsync();

            await _audit.WriteAsync("project.deleted", "Project", id.ToString(),
                new { project.ProjectName });

            return Ok("Project Deleted");
        }


        //get project
        [HttpGet("{id:int}")]
        [Permission(PermissionKeys.ViewProducts)]
        public async Task<ActionResult> GetProjectById(int id)
        {
            var project = await GetAccessibleProjectAsync(id);
            if (project is null) return NotFound();

            var result = await _dbContext
                .Projects
                .AsNoTracking()
                .Where(p => p.ProjectId == id)
                .Select(p => new ProjectResponseDTO
                {
                    ProjectName = p.ProjectName,
                    ProjectId = p.ProjectId,
                    CreatorName = p.Creator!.UserName,
                    Description = p.ProjectDescription,
                    CreatedAt = p.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        //get all project
        [HttpGet("all")]
        [Permission(PermissionKeys.ViewProducts)]
        public async Task<ActionResult> GetProjects()
        {
            var user = await _current.RequireOrgUserAsync();
            var userId = user.UserId;

            var query = _dbContext.Projects.AsNoTracking()
                .Where(p => p.OrganizationId == user.OrganizationId);

            //employees and clients only see projects they were granted access to
            if (!(await IsPrivilegedAsync()))
            {
                query = query.Where(p =>
                    p.CreatorId == userId ||
                    _dbContext.Members.Any(m => m.ProjectId == p.ProjectId && m.UserId == userId));
            }

            var projects = await query
                .Select(p => new ProjectResponseDTO
                {
                    ProjectName = p.ProjectName,
                    ProjectId = p.ProjectId,
                    CreatorName = p.Creator!.UserName,
                    Description = p.ProjectDescription,
                    CreatedAt = p.CreatedAt
                })
                .OrderBy(p => p.ProjectName)
                .ToListAsync();

            return Ok(projects);
        }

        //project members -------------------------------------------------

        [HttpGet("{id:int}/members")]
        [Permission(PermissionKeys.ViewProducts)]
        public async Task<ActionResult> ListMembers(int id)
        {
            var project = await GetAccessibleProjectAsync(id);
            if (project is null) return NotFound();

            var members = await _dbContext.Members.AsNoTracking()
                .Where(m => m.ProjectId == id)
                .OrderBy(m => m.User!.UserName)
                .Select(m => new ProjectMemberResponseDTO
                {
                    UserId = m.UserId,
                    UserName = m.User!.UserName,
                    Role = m.Role,
                    Status = m.User.Status,
                    JoinedOn = m.JoinedOn,
                })
                .ToListAsync();

            return Ok(members);
        }

        [HttpPost("{id:int}/members")]
        [Permission(PermissionKeys.ManageProducts)]
        public async Task<ActionResult> AddMember(int id, AddProjectMemberDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();

            var project = await _dbContext.Projects
                .FirstOrDefaultAsync(p => p.ProjectId == id && p.OrganizationId == user.OrganizationId);
            if (project is null) return NotFound();

            var member = await _dbContext.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == dto.UserId && u.OrganizationId == user.OrganizationId
                    && u.Status == UserStatus.Active);
            if (member is null) return BadRequest("That user is not an active member of this organization.");

            if (await _dbContext.Members.AnyAsync(m => m.ProjectId == id && m.UserId == dto.UserId))
                return BadRequest("Already a member of this project.");

            var roleName = await (
                from ur in _dbContext.UserRoles.AsNoTracking()
                join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.RoleId
                where ur.UserId == dto.UserId && r.OrganizationId == user.OrganizationId
                select r.RoleName
            ).FirstOrDefaultAsync();

            _dbContext.Members.Add(new ProjectMember
            {
                ProjectId = id,
                UserId = dto.UserId,
                Role = dto.Role ?? roleName ?? "Member",
            });
            await _dbContext.SaveChangesAsync();

            await _audit.WriteAsync("project.member.added", "Project", id.ToString(),
                new { member.UserName, project.ProjectName });

            return Ok(new { message = "Member added." });
        }

        [HttpDelete("{id:int}/members/{userId:int}")]
        [Permission(PermissionKeys.ManageProducts)]
        public async Task<ActionResult> RemoveMember(int id, int userId)
        {
            var user = await _current.RequireOrgUserAsync();

            var project = await _dbContext.Projects
                .FirstOrDefaultAsync(p => p.ProjectId == id && p.OrganizationId == user.OrganizationId);
            if (project is null) return NotFound();

            var member = await _dbContext.Members
                .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == userId);
            if (member is null) return NotFound();

            _dbContext.Members.Remove(member);
            await _dbContext.SaveChangesAsync();

            await _audit.WriteAsync("project.member.removed", "Project", id.ToString(),
                new { userId });

            return Ok(new { message = "Member removed." });
        }

        //helpers ---------------------------------------------------------

        private async Task<bool> IsPrivilegedAsync() =>
            _current.Can(PermissionKeys.ManageOrganization) || _current.Can(PermissionKeys.ManageProducts);

        private async Task<Project?> GetAccessibleProjectAsync(int projectId)
        {
            var user = await _current.RequireOrgUserAsync();

            var project = await _dbContext.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.OrganizationId == user.OrganizationId);
            if (project is null) return null;

            if (await IsPrivilegedAsync() || project.CreatorId == user.UserId) return project;

            var isMember = await _dbContext.Members.AsNoTracking()
                .AnyAsync(m => m.ProjectId == projectId && m.UserId == user.UserId);

            return isMember ? project : null;
        }
    }
}
