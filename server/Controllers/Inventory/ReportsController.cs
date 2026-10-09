using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Authorization;
using server.Data;
using server.Helpers;
using server.Services;

namespace server.Controllers.Inventory
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;

        public ReportsController(AppDbContext db, ICurrentUser current)
        {
            _db = db;
            _current = current;
        }

        [Permission(PermissionKeys.ViewReports)]
        [HttpGet("summary")]
        public async Task<ActionResult> GetSummary()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var totalProducts = await _db.Products.CountAsync(p => p.OrganizationId == orgId);
            var totalWarehouses = await _db.Warehouses.CountAsync(w => w.OrganizationId == orgId);
            var lowStock = await (from sb in _db.StockBalances
                                  join p in _db.Products on sb.ProductId equals p.ProductId
                                  where p.OrganizationId == orgId && sb.QuantityOnHand <= p.MinReorderLevel && p.MinReorderLevel > 0
                                  select sb).CountAsync();
            var pendingPO = await _db.PurchaseOrders.CountAsync(po => po.OrganizationId == orgId && (po.Status == Models.Inventory.POStatus.Submitted || po.Status == Models.Inventory.POStatus.Approved));
            var pendingSO = await _db.SalesOrders.CountAsync(so => so.OrganizationId == orgId && (so.Status == Models.Inventory.SOStatus.Confirmed || so.Status == Models.Inventory.SOStatus.PartiallyFulfilled));
            return Ok(new { totalProducts, totalWarehouses, lowStock, pendingPO, pendingSO });
        }
    }
}
