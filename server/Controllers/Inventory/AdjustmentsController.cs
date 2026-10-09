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
    public class AdjustmentsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;
        private readonly IInventoryService _inv;

        public AdjustmentsController(AppDbContext db, ICurrentUser current, IAuditLogService audit, IInventoryService inv)
        {
            _db = db;
            _current = current;
            _audit = audit;
            _inv = inv;
        }

        [Permission(PermissionKeys.StockAdjust)]
        [HttpPost]
        public async Task<ActionResult> Create(AdjustmentCreateDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();
            var orgId = user.OrganizationId!.Value;
            var adj = new StockAdjustment
            {
                OrganizationId = orgId,
                AdjNo = "ADJ-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                WarehouseId = dto.WarehouseId,
                AdjustmentDate = dto.AdjustmentDate,
                Type = dto.Type,
                Reason = dto.Reason,
                Remarks = dto.Remarks,
                AdjustedById = user.UserId
            };
            _db.StockAdjustments.Add(adj);
            await _db.SaveChangesAsync();
            foreach (var it in dto.Items)
            {
                var bal = await _db.StockBalances.FirstOrDefaultAsync(b => b.WarehouseId == dto.WarehouseId && b.ProductId == it.ProductId);
                var oldQty = bal?.QuantityOnHand ?? 0;
                var qty = dto.Type == AdjustmentType.Increase ? it.Quantity : -it.Quantity;
                _db.StockAdjustmentItems.Add(new StockAdjustmentItem { AdjustmentId = adj.AdjustmentId, ProductId = it.ProductId, Quantity = it.Quantity, OldQty = oldQty, NewQty = oldQty + qty });
                await _inv.PostStockMovementAsync(orgId, it.ProductId, dto.WarehouseId, MovementType.ADJUST, ReferenceType.ADJUST, adj.AdjNo, qty, null, dto.Reason, user.UserId);
            }
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("adjustment.created", "Adjustment", adj.AdjustmentId.ToString());
            return Ok(new { adj.AdjustmentId });
        }
    }
}
