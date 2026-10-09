namespace server.Models.Inventory
{
    public enum TransferStatus
    {
        Pending = 0,
        Completed = 1,
        Cancelled = 2
    }

    public class StockTransfer
    {
        public int TransferId { get; set; }
        public string TransferNo { get; set; } = string.Empty;
        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        public int FromWarehouseId { get; set; }
        public Warehouse? FromWarehouse { get; set; }

        public int ToWarehouseId { get; set; }
        public Warehouse? ToWarehouse { get; set; }

        public DateTime TransferDate { get; set; } = DateTime.UtcNow;
        public TransferStatus Status { get; set; } = TransferStatus.Pending;

        public string? Reason { get; set; }
        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<StockTransferItem>? Items { get; set; }
    }

    public class StockTransferItem
    {
        public int TransferItemId { get; set; }
        public int TransferId { get; set; }
        public StockTransfer? Transfer { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public decimal Quantity { get; set; }
    }
}
