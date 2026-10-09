namespace server.Models.Inventory
{
    public enum POStatus
    {
        Draft = 0,
        Submitted = 1,
        Approved = 2,
        PartiallyReceived = 3,
        Received = 4,
        Cancelled = 5
    }

    public class PurchaseOrder
    {
        public int POId { get; set; }
        public string PONumber { get; set; } = string.Empty;
        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public DateTime? ExpectedDate { get; set; }
        public POStatus Status { get; set; } = POStatus.Draft;

        public string? Notes { get; set; }
        public decimal TotalAmount { get; set; }

        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public int? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<PurchaseOrderItem>? Items { get; set; }
    }

    public class PurchaseOrderItem
    {
        public int POItemId { get; set; }
        public int POId { get; set; }
        public PurchaseOrder? PO { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public decimal Quantity { get; set; }
        public decimal ReceivedQty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
