using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Authorization;
using server.Data;
using server.DTOs.Inventory;
using server.Helpers;
using server.Services;

namespace server.Controllers.Inventory
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;

        public InventoryController(AppDbContext db, ICurrentUser current)
        {
            _db = db;
            _current = current;
        }

        [Permission(PermissionKeys.ViewStock)]
        [HttpGet("stock")]
        public async Task<ActionResult> GetStock()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var list = await (from sb in _db.StockBalances.AsNoTracking()
                              join w in _db.Warehouses on sb.WarehouseId equals w.WarehouseId
                              join p in _db.Products on sb.ProductId equals p.ProductId
                              where w.OrganizationId == orgId && p.OrganizationId == orgId
                              select new StockBalanceDTO
                              {
                                  WarehouseId = sb.WarehouseId,
                                  WarehouseName = w.Name,
                                  ProductId = sb.ProductId,
                                  SKU = p.SKU,
                                  ProductName = p.Name,
                                  QuantityOnHand = sb.QuantityOnHand,
                                  ReservedQty = sb.ReservedQty,
                                  AvailableQty = sb.QuantityOnHand - sb.ReservedQty
                              })
                              .OrderBy(x => x.WarehouseName).ThenBy(x => x.ProductName)
                              .ToListAsync();
            return Ok(list);
        }

        [Permission(PermissionKeys.ViewStock)]
        [HttpGet("low-stock")]
        public async Task<ActionResult> GetLowStock()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var list = await (from sb in _db.StockBalances.AsNoTracking()
                              join p in _db.Products on sb.ProductId equals p.ProductId
                              where p.OrganizationId == orgId && sb.QuantityOnHand <= p.MinReorderLevel && p.MinReorderLevel > 0
                              select new LowStockDTO
                              {
                                  ProductId = p.ProductId,
                                  SKU = p.SKU,
                                  ProductName = p.Name,
                                  MinReorderLevel = p.MinReorderLevel,
                                  QuantityOnHand = sb.QuantityOnHand
                              })
                              .OrderBy(x => x.ProductName)
                              .ToListAsync();
            return Ok(list);
        }

        [Permission(PermissionKeys.ViewStock)]
        [HttpGet("movements")]
        public async Task<ActionResult> GetMovements([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 200);
            var q = _db.StockMovements.AsNoTracking()
                .Where(m => m.OrganizationId == orgId)
                .OrderByDescending(m => m.MovedAt).ThenByDescending(m => m.StockMovementId);
            var total = await q.CountAsync();
            var items = await q.Skip((page-1)*pageSize).Take(pageSize)
                .Select(m => new StockMovementDTO
                {
                    StockMovementId = m.StockMovementId,
                    ProductName = m.Product!.Name,
                    SKU = m.Product!.SKU,
                    WarehouseName = m.Warehouse!.Name,
                    MovementType = m.MovementType.ToString(),
                    ReferenceType = m.ReferenceType.ToString(),
                    ReferenceId = m.ReferenceId,
                    Quantity = m.Quantity,
                    UnitCost = m.UnitCost,
                    Remarks = m.Remarks,
                    MovedByName = m.MovedBy!.UserName,
                    MovedAt = m.MovedAt
                })
                .ToListAsync();
            return Ok(new { total, page, pageSize, items });
        }
    }
}
