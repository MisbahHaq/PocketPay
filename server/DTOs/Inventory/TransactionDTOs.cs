namespace server.DTOs.Inventory
{
    public class PODTO
    {
        public int POId { get; set; }
        public string PONumber { get; set; } = string.Empty;
        public string? SupplierName { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string? CreatedByName { get; set; }
    }

    public class POCreateDTO
    {
        public int SupplierId { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string? Notes { get; set; }
        public List<POItemDTO> Items { get; set; } = new();
    }

    public class POItemDTO
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class GRNCreateDTO
    {
        public int? POId { get; set; }
        public int SupplierId { get; set; }
        public int WarehouseId { get; set; }
        public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
        public List<GRNItemDTO> Items { get; set; } = new();
    }

    public class GRNItemDTO
    {
        public int ProductId { get; set; }
        public decimal QuantityReceived { get; set; }
        public string? BatchNo { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }
}
