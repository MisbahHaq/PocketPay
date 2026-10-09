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
    public class SuppliersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;

        public SuppliersController(AppDbContext db, ICurrentUser current, IAuditLogService audit)
        {
            _db = db;
            _current = current;
            _audit = audit;
        }

        [Permission(PermissionKeys.ViewSuppliers)]
        [HttpGet]
        public async Task<ActionResult> GetAll()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var list = await _db.Suppliers.AsNoTracking()
                .Where(s => s.OrganizationId == orgId)
                .OrderBy(s => s.Name)
                .Select(s => new SupplierDTO { SupplierId = s.SupplierId, Name = s.Name, ContactPerson = s.ContactPerson, Email = s.Email, Phone = s.Phone, IsActive = s.IsActive })
                .ToListAsync();
            return Ok(list);
        }

        [Permission(PermissionKeys.ManageSuppliers)]
        [HttpPost]
        public async Task<ActionResult> Create(SupplierUpsertDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var s = new Supplier { OrganizationId = orgId, Name = dto.Name.Trim(), ContactPerson = dto.ContactPerson, Email = dto.Email, Phone = dto.Phone, TaxNo = dto.TaxNo, Address = dto.Address, PaymentTerms = dto.PaymentTerms, IsActive = dto.IsActive, CreatedAt = DateTime.UtcNow };
            _db.Suppliers.Add(s);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("supplier.created", "Supplier", s.SupplierId.ToString());
            return Ok(new { s.SupplierId });
        }

        [Permission(PermissionKeys.ManageSuppliers)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, SupplierUpsertDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.OrganizationId == orgId && x.SupplierId == id);
            if (s == null) return NotFound();
            s.Name = dto.Name.Trim();
            s.ContactPerson = dto.ContactPerson;
            s.Email = dto.Email;
            s.Phone = dto.Phone;
            s.TaxNo = dto.TaxNo;
            s.Address = dto.Address;
            s.PaymentTerms = dto.PaymentTerms;
            s.IsActive = dto.IsActive;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("supplier.updated", "Supplier", s.SupplierId.ToString());
            return Ok();
        }
    }
}
