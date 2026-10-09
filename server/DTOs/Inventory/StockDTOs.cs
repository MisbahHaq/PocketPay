namespace server.DTOs.Inventory
{
    public class StockBalanceDTO
    {
        public int WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public int ProductId { get; set; }
        public string? SKU { get; set; }
        public string? ProductName { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal ReservedQty { get; set; }
        public decimal AvailableQty { get; set; }
    }

    public class LowStockDTO
    {
        public int ProductId { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int MinReorderLevel { get; set; }
        public decimal QuantityOnHand { get; set; }
    }

    public class StockMovementDTO
    {
        public long StockMovementId { get; set; }
        public string? ProductName { get; set; }
        public string? SKU { get; set; }
        public string? WarehouseName { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public string ReferenceType { get; set; } = string.Empty;
        public string? ReferenceId { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitCost { get; set; }
        public string? Remarks { get; set; }
        public string? MovedByName { get; set; }
        public DateTime MovedAt { get; set; }
    }
}
