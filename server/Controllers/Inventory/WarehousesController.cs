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
    public class WarehousesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;

        public WarehousesController(AppDbContext db, ICurrentUser current, IAuditLogService audit)
        {
            _db = db;
            _current = current;
            _audit = audit;
        }

        [Permission(PermissionKeys.ViewWarehouses)]
        [HttpGet]
        public async Task<ActionResult> GetAll()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var list = await _db.Warehouses.AsNoTracking()
                .Where(w => w.OrganizationId == orgId)
                .OrderBy(w => w.Name)
                .Select(w => new WarehouseDTO { WarehouseId = w.WarehouseId, Name = w.Name, Code = w.Code, Location = w.Location, IsActive = w.IsActive })
                .ToListAsync();
            return Ok(list);
        }

        [Permission(PermissionKeys.ManageWarehouses)]
        [HttpPost]
        public async Task<ActionResult> Create(WarehouseDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var w = new Warehouse { OrganizationId = orgId, Name = dto.Name.Trim(), Code = dto.Code.Trim(), Location = dto.Location, IsActive = dto.IsActive, CreatedAt = DateTime.UtcNow };
            _db.Warehouses.Add(w);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("warehouse.created", "Warehouse", w.WarehouseId.ToString());
            return Ok(new { w.WarehouseId });
        }

        [Permission(PermissionKeys.ManageWarehouses)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, WarehouseDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var w = await _db.Warehouses.FirstOrDefaultAsync(x => x.OrganizationId == orgId && x.WarehouseId == id);
            if (w == null) return NotFound();
            w.Name = dto.Name.Trim();
            w.Code = dto.Code.Trim();
            w.Location = dto.Location;
            w.IsActive = dto.IsActive;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("warehouse.updated", "Warehouse", w.WarehouseId.ToString());
            return Ok();
        }
    }
}
