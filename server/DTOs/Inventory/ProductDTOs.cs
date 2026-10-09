namespace server.DTOs.Inventory
{
    public class ProductDTO
    {
        public int ProductId { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public int UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int MinReorderLevel { get; set; }
        public int MaxReorderLevel { get; set; }
        public bool IsActive { get; set; }
        public bool HasExpiry { get; set; }
        public bool TrackBatch { get; set; }
    }

    public class ProductUpsertDTO
    {
        public string SKU { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public int UnitId { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int MinReorderLevel { get; set; }
        public int MaxReorderLevel { get; set; }
        public bool IsActive { get; set; } = true;
        public bool HasExpiry { get; set; } = false;
        public bool TrackBatch { get; set; } = false;
    }
}
