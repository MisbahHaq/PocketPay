namespace server.Models.Inventory
{
    public enum SOStatus
    {
        Draft = 0,
        Confirmed = 1,
        PartiallyFulfilled = 2,
        Fulfilled = 3,
        Cancelled = 4
    }

    public class SalesOrder
    {
        public int SOId { get; set; }
        public string SONumber { get; set; } = string.Empty;
        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public SOStatus Status { get; set; } = SOStatus.Draft;

        public int WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }

        public string? Notes { get; set; }
        public decimal TotalAmount { get; set; }

        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<SalesOrderItem>? Items { get; set; }
    }

    public class SalesOrderItem
    {
        public int SOItemId { get; set; }
        public int SOId { get; set; }
        public SalesOrder? SO { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public decimal Quantity { get; set; }
        public decimal FulfilledQty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
