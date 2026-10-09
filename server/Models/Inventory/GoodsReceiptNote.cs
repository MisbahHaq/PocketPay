namespace server.Models.Inventory
{
    public enum GRNStatus
    {
        Draft = 0,
        Received = 1,
        Cancelled = 2
    }

    public class GoodsReceiptNote
    {
        public int GRNId { get; set; }
        public string GRNNumber { get; set; } = string.Empty;
        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        public int? POId { get; set; }
        public PurchaseOrder? PO { get; set; }

        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }

        public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
        public int ReceivedById { get; set; }
        public User? ReceivedBy { get; set; }

        public string? Notes { get; set; }
        public GRNStatus Status { get; set; } = GRNStatus.Received;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<GoodsReceiptItem>? Items { get; set; }
    }

    public class GoodsReceiptItem
    {
        public int GRNItemId { get; set; }
        public int GRNId { get; set; }
        public GoodsReceiptNote? GRN { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public decimal QuantityReceived { get; set; }
        public string? BatchNo { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }
}
