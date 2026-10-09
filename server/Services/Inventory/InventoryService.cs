using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models.Inventory;

namespace server.Services.Inventory
{
    public interface IInventoryService
    {
        Task PostStockMovementAsync(
            int orgId,
            int productId,
            int warehouseId,
            MovementType movementType,
            ReferenceType referenceType,
            string? referenceId,
            decimal quantity,
            decimal? unitCost,
            string? remarks,
            int? movedByUserId,
            CancellationToken ct = default);
    }

    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _db;

        public InventoryService(AppDbContext db)
        {
            _db = db;
        }

        public async Task PostStockMovementAsync(
            int orgId,
            int productId,
            int warehouseId,
            MovementType movementType,
            ReferenceType referenceType,
            string? referenceId,
            decimal quantity,
            decimal? unitCost,
            string? remarks,
            int? movedByUserId,
            CancellationToken ct = default)
        {
            var movement = new StockMovement
            {
                OrganizationId = orgId,
                ProductId = productId,
                WarehouseId = warehouseId,
                MovementType = movementType,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                Quantity = quantity,
                UnitCost = unitCost,
                Remarks = remarks,
                MovedByUserId = movedByUserId,
                MovedAt = DateTime.UtcNow
            };
            _db.StockMovements.Add(movement);

            var balance = await _db.StockBalances
                .FirstOrDefaultAsync(sb => sb.WarehouseId == warehouseId && sb.ProductId == productId, ct);

            if (balance == null)
            {
                balance = new StockBalance
                {
                    WarehouseId = warehouseId,
                    ProductId = productId,
                    QuantityOnHand = 0,
                    ReservedQty = 0,
                    LastUpdatedAt = DateTime.UtcNow
                };
                _db.StockBalances.Add(balance);
            }

            balance.QuantityOnHand += quantity;
            balance.LastUpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }
    }
}
