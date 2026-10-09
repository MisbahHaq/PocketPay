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
    public class TransfersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;
        private readonly IInventoryService _inv;

        public TransfersController(AppDbContext db, ICurrentUser current, IAuditLogService audit, IInventoryService inv)
        {
            _db = db;
            _current = current;
            _audit = audit;
            _inv = inv;
        }

        [Permission(PermissionKeys.StockTransfer)]
        [HttpPost]
        public async Task<ActionResult> Create(TransferCreateDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();
            var orgId = user.OrganizationId!.Value;
            if (dto.FromWarehouseId == dto.ToWarehouseId) return BadRequest("From and To warehouses cannot be same.");
            var tr = new StockTransfer
            {
                OrganizationId = orgId,
                TransferNo = "TR-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                FromWarehouseId = dto.FromWarehouseId,
                ToWarehouseId = dto.ToWarehouseId,
                TransferDate = dto.TransferDate,
                Status = TransferStatus.Pending,
                Reason = dto.Reason,
                CreatedById = user.UserId
            };
            _db.StockTransfers.Add(tr);
            await _db.SaveChangesAsync();
            foreach (var it in dto.Items)
            {
                _db.StockTransferItems.Add(new StockTransferItem { TransferId = tr.TransferId, ProductId = it.ProductId, Quantity = it.Quantity });
                var balFrom = await _db.StockBalances.FirstOrDefaultAsync(b => b.WarehouseId == dto.FromWarehouseId && b.ProductId == it.ProductId);
                if (balFrom == null || balFrom.QuantityOnHand < it.Quantity) return BadRequest("Insufficient stock in source.");
            }
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("transfer.created", "Transfer", tr.TransferId.ToString());
            return Ok(new { tr.TransferId });
        }

        [Permission(PermissionKeys.StockTransfer)]
        [HttpPost("{id:int}/complete")]
        public async Task<ActionResult> Complete(int id)
        {
            var user = await _current.RequireOrgUserAsync();
            var orgId = user.OrganizationId!.Value;
            var tr = await _db.StockTransfers.Include(t => t.Items).FirstOrDefaultAsync(t => t.OrganizationId == orgId && t.TransferId == id);
            if (tr == null) return NotFound();
            if (tr.Status != TransferStatus.Pending) return BadRequest("Invalid status.");
            foreach (var it in tr.Items!)
            {
                await _inv.PostStockMovementAsync(orgId, it.ProductId, tr.FromWarehouseId, MovementType.TRANSFER, ReferenceType.TRANSFER, tr.TransferNo, -it.Quantity, null, "Transfer out", user.UserId);
                await _inv.PostStockMovementAsync(orgId, it.ProductId, tr.ToWarehouseId, MovementType.TRANSFER, ReferenceType.TRANSFER, tr.TransferNo, it.Quantity, null, "Transfer in", user.UserId);
            }
            tr.Status = TransferStatus.Completed;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("transfer.completed", "Transfer", tr.TransferId.ToString());
            return Ok();
        }
    }
}
