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
    public class GRNController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;
        private readonly IInventoryService _inv;

        public GRNController(AppDbContext db, ICurrentUser current, IAuditLogService audit, IInventoryService inv)
        {
            _db = db;
            _current = current;
            _audit = audit;
            _inv = inv;
        }

        [Permission(PermissionKeys.ReceiveGRN)]
        [HttpPost]
        public async Task<ActionResult> Create(GRNCreateDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();
            var orgId = user.OrganizationId!.Value;
            PurchaseOrder? po = null;
            if (dto.POId.HasValue)
            {
                po = await _db.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.POId == dto.POId.Value);
                if (po == null) return NotFound("PO not found.");
                if (po.Status != POStatus.Approved && po.Status != POStatus.PartiallyReceived)
                    return BadRequest("PO not eligible for receiving.");
            }
            var grn = new GoodsReceiptNote
            {
                OrganizationId = orgId,
                GRNNumber = "GRN-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                POId = dto.POId,
                SupplierId = dto.SupplierId,
                WarehouseId = dto.WarehouseId,
                ReceiptDate = dto.ReceiptDate,
                ReceivedById = user.UserId,
                Notes = dto.Notes,
                Status = GRNStatus.Received
            };
            _db.GoodsReceiptNotes.Add(grn);
            await _db.SaveChangesAsync();
            foreach (var it in dto.Items)
            {
                _db.GoodsReceiptItems.Add(new GoodsReceiptItem { GRNId = grn.GRNId, ProductId = it.ProductId, QuantityReceived = it.QuantityReceived, BatchNo = it.BatchNo, ExpiryDate = it.ExpiryDate });
                await _inv.PostStockMovementAsync(orgId, it.ProductId, dto.WarehouseId, MovementType.IN, ReferenceType.GRN, grn.GRNNumber, it.QuantityReceived, null, "GRN received", user.UserId);
            }
            if (po != null)
            {
                foreach (var pit in po.Items!)
                {
                    var recv = dto.Items.Where(i => i.ProductId == pit.ProductId).Sum(i => i.QuantityReceived);
                    pit.ReceivedQty += recv;
                }
                var allReceived = po.Items!.All(i => i.ReceivedQty >= i.Quantity);
                po.Status = allReceived ? POStatus.Received : POStatus.PartiallyReceived;
            }
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("grn.created", "GRN", grn.GRNId.ToString());
            return Ok(new { grn.GRNId });
        }
    }
}
