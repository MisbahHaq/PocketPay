using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Authorization;
using server.Data;
using server.DTOs.Inventory;
using server.Helpers;
using server.Models.Inventory;
using server.Services;

namespace server.Controllers.Inventory
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;

        public CategoriesController(AppDbContext db, ICurrentUser current, IAuditLogService audit)
        {
            _db = db;
            _current = current;
            _audit = audit;
        }

        [Permission(PermissionKeys.ViewCategories)]
        [HttpGet]
        public async Task<ActionResult> GetAll()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var list = await _db.Categories.AsNoTracking()
                .Where(c => c.OrganizationId == orgId)
                .OrderBy(c => c.Name)
                .Select(c => new CategoryDTO { CategoryId = c.CategoryId, Name = c.Name, Code = c.Code, IsActive = c.IsActive })
                .ToListAsync();
            return Ok(list);
        }

        [Permission(PermissionKeys.ManageCategories)]
        [HttpPost]
        public async Task<ActionResult> Create(CategoryDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var c = new Category { OrganizationId = orgId, Name = dto.Name.Trim(), Code = dto.Code, IsActive = dto.IsActive, CreatedAt = DateTime.UtcNow };
            _db.Categories.Add(c);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("category.created", "Category", c.CategoryId.ToString());
            return Ok(new { c.CategoryId });
        }

        [Permission(PermissionKeys.ManageCategories)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, CategoryDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var c = await _db.Categories.FirstOrDefaultAsync(x => x.OrganizationId == orgId && x.CategoryId == id);
            if (c == null) return NotFound();
            c.Name = dto.Name.Trim();
            c.Code = dto.Code;
            c.IsActive = dto.IsActive;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("category.updated", "Category", c.CategoryId.ToString());
            return Ok();
        }
    }
}
