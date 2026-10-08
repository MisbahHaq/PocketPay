using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.DTOs.ProjectDTO;
using server.Models;

namespace server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        public ProjectsController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        //create project
        [HttpPost("create")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult> CreateProject(CreateProjectDTO dto)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var project = new Project
            {
                ProjectName = dto.ProjectName,
                ProjectDescription = dto.Description,
                CreatorId = userId
            };

            await _dbContext.Projects.AddAsync(project);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Project Create", project.ProjectId });
        }

        //update project
        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<ActionResult> UpdateProjectById(int id,CreateProjectDTO dto)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var project = await _dbContext.Projects.FindAsync(id);

            if (project == null)
            {
                return NotFound();
            }

            if (project.CreatorId != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            project.ProjectName = dto.ProjectName;
            project.ProjectDescription = dto.Description;
            await _dbContext.SaveChangesAsync();


            return Ok(project);
        }

        //delete project
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<ActionResult> DeleteProjectById(int id)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var project = await _dbContext.Projects.FindAsync(id);

            if (project == null)
            {
                return NotFound();
            }

            if (project.CreatorId != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            _dbContext.Projects.Remove(project);
            await _dbContext.SaveChangesAsync();


            return Ok("Project Deleted");
        }


        //get project
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<ActionResult> GetProjectById(int id)
        {
            var project = await _dbContext
                .Projects
                .AsNoTracking()
                .Select(p => new ProjectResponseDTO
                {
                    ProjectName = p.ProjectName,
                    ProjectId = p.ProjectId,
                    CreatorName = p.Creator!.UserName,
                    Description = p.ProjectDescription
                })
                .FirstOrDefaultAsync(p => p.ProjectId == id);

            if(project == null)
            {
                return NotFound();
            }

            return Ok(project);
        }

        //get all project
        [HttpGet("all")]
        [Authorize]
        public async Task<ActionResult> GetProjects(int id)
        {
            var projects = await _dbContext
                .Projects
                .AsNoTracking()
                .Select(p => new ProjectResponseDTO
                {
                    ProjectName = p.ProjectName,
                    ProjectId = p.ProjectId,
                    CreatorName = p.Creator!.UserName,
                    Description = p.ProjectDescription
                })
                .ToListAsync();

            if (projects == null)
            {
                return NotFound();
            }

            return Ok(projects);
        }
    }
}
