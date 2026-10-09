namespace server.Models.Inventory
{
    public class StockBalance
    {
        public int WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public decimal QuantityOnHand { get; set; } = 0;
        public decimal ReservedQty { get; set; } = 0;
        public decimal AvailableQty => QuantityOnHand - ReservedQty;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
