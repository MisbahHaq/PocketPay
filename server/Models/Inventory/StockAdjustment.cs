namespace server.Models.Inventory
{
    public enum AdjustmentType
    {
        Increase = 0,
        Decrease = 1
    }

    public class StockAdjustment
    {
        public int AdjustmentId { get; set; }
        public string AdjNo { get; set; } = string.Empty;
        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }

        public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow;
        public AdjustmentType Type { get; set; }
        public string? Reason { get; set; }
        public string? Remarks { get; set; }

        public int AdjustedById { get; set; }
        public User? AdjustedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<StockAdjustmentItem>? Items { get; set; }
    }

    public class StockAdjustmentItem
    {
        public int AdjustmentItemId { get; set; }
        public int AdjustmentId { get; set; }
        public StockAdjustment? Adjustment { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public decimal Quantity { get; set; }
        public decimal OldQty { get; set; }
        public decimal NewQty { get; set; }
    }
}
