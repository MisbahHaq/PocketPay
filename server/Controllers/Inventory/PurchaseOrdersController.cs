using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Authorization;
using server.Data;
using server.DTOs.Inventory;
using server.Helpers;
using server.Models.Inventory;
using server.Services;
using server.Services.Inventory;

namespace server.Controllers.Inventory
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchaseOrdersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;
        private readonly IInventoryService _inv;

        public PurchaseOrdersController(AppDbContext db, ICurrentUser current, IAuditLogService audit, IInventoryService inv)
        {
            _db = db;
            _current = current;
            _audit = audit;
            _inv = inv;
        }

        [Permission(PermissionKeys.ViewPO)]
        [HttpGet]
        public async Task<ActionResult> GetAll()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var list = await _db.PurchaseOrders.AsNoTracking()
                .Where(p => p.OrganizationId == orgId)
                .OrderByDescending(p => p.POId)
                .Select(p => new PODTO
                {
                    POId = p.POId,
                    PONumber = p.PONumber,
                    SupplierName = p.Supplier!.Name,
                    OrderDate = p.OrderDate,
                    ExpectedDate = p.ExpectedDate,
                    Status = p.Status.ToString(),
                    TotalAmount = p.TotalAmount,
                    CreatedByName = p.CreatedBy!.UserName
                })
                .ToListAsync();
            return Ok(list);
        }

        [Permission(PermissionKeys.CreatePO)]
        [HttpPost]
        public async Task<ActionResult> Create(POCreateDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();
            var orgId = user.OrganizationId!.Value;
            var po = new PurchaseOrder
            {
                OrganizationId = orgId,
                PONumber = "PO-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                SupplierId = dto.SupplierId,
                OrderDate = DateTime.UtcNow,
                ExpectedDate = dto.ExpectedDate,
                Status = POStatus.Draft,
                Notes = dto.Notes,
                CreatedById = user.UserId,
                TotalAmount = dto.Items.Sum(i => i.Quantity * i.UnitPrice)
            };
            _db.PurchaseOrders.Add(po);
            await _db.SaveChangesAsync();
            foreach (var it in dto.Items)
            {
                _db.PurchaseOrderItems.Add(new PurchaseOrderItem
                {
                    POId = po.POId,
                    ProductId = it.ProductId,
                    Quantity = it.Quantity,
                    ReceivedQty = 0,
                    UnitPrice = it.UnitPrice,
                    LineTotal = it.Quantity * it.UnitPrice
                });
            }
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("po.created", "PO", po.POId.ToString());
            return Ok(new { po.POId });
        }

        [Permission(PermissionKeys.ApprovePO)]
        [HttpPost("{id:int}/approve")]
        public async Task<ActionResult> Approve(int id)
        {
            var user = await _current.RequireOrgUserAsync();
            var orgId = user.OrganizationId!.Value;
            var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.POId == id);
            if (po == null) return NotFound();
            if (po.Status != POStatus.Submitted && po.Status != POStatus.Draft)
                return BadRequest("Invalid status.");
            po.Status = POStatus.Approved;
            po.ApprovedById = user.UserId;
            po.ApprovedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("po.approved", "PO", po.POId.ToString());
            return Ok();
        }

        [Permission(PermissionKeys.CreatePO)]
        [HttpPost("{id:int}/submit")]
        public async Task<ActionResult> Submit(int id)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.POId == id);
            if (po == null) return NotFound();
            if (po.Status != POStatus.Draft) return BadRequest("Invalid status.");
            po.Status = POStatus.Submitted;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("po.submitted", "PO", po.POId.ToString());
            return Ok();
        }
    }
}
