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
    public class SalesOrdersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;
        private readonly IInventoryService _inv;

        public SalesOrdersController(AppDbContext db, ICurrentUser current, IAuditLogService audit, IInventoryService inv)
        {
            _db = db;
            _current = current;
            _audit = audit;
            _inv = inv;
        }

        [Permission(PermissionKeys.ViewSO)]
        [HttpGet]
        public async Task<ActionResult> GetAll()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var list = await _db.SalesOrders.AsNoTracking()
                .Where(s => s.OrganizationId == orgId)
                .OrderByDescending(s => s.SOId)
                .Select(s => new { s.SOId, s.SONumber, CustomerName = s.Customer!.Name, s.OrderDate, Status = s.Status.ToString(), s.TotalAmount, CreatedByName = s.CreatedBy!.UserName })
                .ToListAsync();
            return Ok(list);
        }

        [Permission(PermissionKeys.CreateSO)]
        [HttpPost]
        public async Task<ActionResult> Create(SOCreateDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();
            var orgId = user.OrganizationId!.Value;
            var so = new SalesOrder
            {
                OrganizationId = orgId,
                SONumber = "SO-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                CustomerId = dto.CustomerId,
                WarehouseId = dto.WarehouseId,
                OrderDate = dto.OrderDate,
                Status = SOStatus.Confirmed,
                Notes = dto.Notes,
                CreatedById = user.UserId,
                TotalAmount = dto.Items.Sum(i => i.Quantity * i.UnitPrice)
            };
            _db.SalesOrders.Add(so);
            await _db.SaveChangesAsync();
            foreach (var it in dto.Items)
            {
                _db.SalesOrderItems.Add(new SalesOrderItem
                {
                    SOId = so.SOId,
                    ProductId = it.ProductId,
                    Quantity = it.Quantity,
                    FulfilledQty = 0,
                    UnitPrice = it.UnitPrice,
                    LineTotal = it.Quantity * it.UnitPrice
                });
            }
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("so.created", "SO", so.SOId.ToString());
            return Ok(new { so.SOId });
        }

        [Permission(PermissionKeys.FulfillSO)]
        [HttpPost("{id:int}/fulfill")]
        public async Task<ActionResult> Fulfill(int id)
        {
            var user = await _current.RequireOrgUserAsync();
            var orgId = user.OrganizationId!.Value;
            var so = await _db.SalesOrders.Include(s => s.Items).FirstOrDefaultAsync(s => s.OrganizationId == orgId && s.SOId == id);
            if (so == null) return NotFound();
            if (so.Status == SOStatus.Fulfilled || so.Status == SOStatus.Cancelled) return BadRequest("Invalid status.");
            foreach (var it in so.Items!)
            {
                var bal = await _db.StockBalances.FirstOrDefaultAsync(b => b.WarehouseId == so.WarehouseId && b.ProductId == it.ProductId);
                if (bal == null || bal.QuantityOnHand < it.Quantity - it.FulfilledQty) return BadRequest("Insufficient stock for " + it.ProductId);
                var fulfillQty = it.Quantity - it.FulfilledQty;
                it.FulfilledQty += fulfillQty;
                await _inv.PostStockMovementAsync(orgId, it.ProductId, so.WarehouseId, MovementType.OUT, ReferenceType.SO, so.SONumber, -fulfillQty, null, "SO fulfilled", user.UserId);
            }
            var allFulfilled = so.Items!.All(i => i.FulfilledQty >= i.Quantity);
            so.Status = allFulfilled ? SOStatus.Fulfilled : SOStatus.PartiallyFulfilled;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("so.fulfilled", "SO", so.SOId.ToString());
            return Ok();
        }
    }
}
