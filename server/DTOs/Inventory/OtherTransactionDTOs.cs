namespace server.DTOs.Inventory
{
    public class SOCreateDTO
    {
        public int CustomerId { get; set; }
        public int WarehouseId { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
        public List<SOItemDTO> Items { get; set; } = new();
    }

    public class SOItemDTO
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class TransferCreateDTO
    {
        public int FromWarehouseId { get; set; }
        public int ToWarehouseId { get; set; }
        public DateTime TransferDate { get; set; } = DateTime.UtcNow;
        public string? Reason { get; set; }
        public List<TransferItemDTO> Items { get; set; } = new();
    }

    public class TransferItemDTO
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class AdjustmentCreateDTO
    {
        public int WarehouseId { get; set; }
        public server.Models.Inventory.AdjustmentType Type { get; set; }
        public string? Reason { get; set; }
        public string? Remarks { get; set; }
        public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow;
        public List<AdjustmentItemDTO> Items { get; set; } = new();
    }

    public class AdjustmentItemDTO
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
    }
}
