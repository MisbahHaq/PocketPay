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
    public class CustomersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;

        public CustomersController(AppDbContext db, ICurrentUser current, IAuditLogService audit)
        {
            _db = db;
            _current = current;
            _audit = audit;
        }

        [Permission(PermissionKeys.ViewCustomers)]
        [HttpGet]
        public async Task<ActionResult> GetAll()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var list = await _db.Customers.AsNoTracking()
                .Where(c => c.OrganizationId == orgId)
                .OrderBy(c => c.Name)
                .Select(c => new CustomerDTO { CustomerId = c.CustomerId, Name = c.Name, Email = c.Email, Phone = c.Phone, IsActive = c.IsActive })
                .ToListAsync();
            return Ok(list);
        }

        [Permission(PermissionKeys.ManageCustomers)]
        [HttpPost]
        public async Task<ActionResult> Create(CustomerUpsertDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var c = new Customer { OrganizationId = orgId, Name = dto.Name.Trim(), Email = dto.Email, Phone = dto.Phone, Address = dto.Address, IsActive = dto.IsActive, CreatedAt = DateTime.UtcNow };
            _db.Customers.Add(c);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("customer.created", "Customer", c.CustomerId.ToString());
            return Ok(new { c.CustomerId });
        }

        [Permission(PermissionKeys.ManageCustomers)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, CustomerUpsertDTO dto)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var c = await _db.Customers.FirstOrDefaultAsync(x => x.OrganizationId == orgId && x.CustomerId == id);
            if (c == null) return NotFound();
            c.Name = dto.Name.Trim();
            c.Email = dto.Email;
            c.Phone = dto.Phone;
            c.Address = dto.Address;
            c.IsActive = dto.IsActive;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("customer.updated", "Customer", c.CustomerId.ToString());
            return Ok();
        }
    }
}
