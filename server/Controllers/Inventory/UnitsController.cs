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
    public class UnitsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;

        public UnitsController(AppDbContext db, ICurrentUser current, IAuditLogService audit)
        {
            _db = db;
            _current = current;
            _audit = audit;
        }

        [Permission(PermissionKeys.ViewUnits)]
        [HttpGet]
        public async Task<ActionResult> GetAll()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var list = await _db.UnitsOfMeasure.AsNoTracking()
                .Where(u => u.OrganizationId == orgId)
                .OrderBy(u => u.Name)
                .Select(u => new UnitDTO { UnitId = u.UnitId, Name = u.Name, Code = u.Code, IsActive = u.IsActive })
                .ToListAsync();
            return Ok(list);
        }

        [Permission(PermissionKeys.ManageUnits)]
        [HttpPost]
        public async Task<ActionResult> Create(UnitDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var u = new UnitOfMeasure { OrganizationId = orgId, Name = dto.Name.Trim(), Code = dto.Code.Trim(), IsActive = dto.IsActive, CreatedAt = DateTime.UtcNow };
            _db.UnitsOfMeasure.Add(u);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("unit.created", "Unit", u.UnitId.ToString());
            return Ok(new { u.UnitId });
        }

        [Permission(PermissionKeys.ManageUnits)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, UnitDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var u = await _db.UnitsOfMeasure.FirstOrDefaultAsync(x => x.OrganizationId == orgId && x.UnitId == id);
            if (u == null) return NotFound();
            u.Name = dto.Name.Trim();
            u.Code = dto.Code.Trim();
            u.IsActive = dto.IsActive;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("unit.updated", "Unit", u.UnitId.ToString());
            return Ok();
        }
    }
}
