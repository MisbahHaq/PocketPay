namespace server.Models.Inventory
{
    public class Product
    {
        public int ProductId { get; set; }
        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        public string SKU { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public int UnitId { get; set; }
        public UnitOfMeasure? Unit { get; set; }

        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int MinReorderLevel { get; set; } = 0;
        public int MaxReorderLevel { get; set; } = 0;

        public bool IsActive { get; set; } = true;
        public bool HasExpiry { get; set; } = false;
        public bool TrackBatch { get; set; } = false;

        public int? CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<StockBalance>? StockBalances { get; set; }
    }
}
