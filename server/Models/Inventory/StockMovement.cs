namespace server.Models.Inventory
{
    public enum MovementType
    {
        IN = 0,
        OUT = 1,
        ADJUST = 2,
        TRANSFER = 3
    }

    public enum ReferenceType
    {
        PO = 0,
        GRN = 1,
        SO = 2,
        ADJUST = 3,
        TRANSFER = 4
    }

    public class StockMovement
    {
        public long StockMovementId { get; set; }
        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }

        public MovementType MovementType { get; set; }
        public ReferenceType ReferenceType { get; set; }
        public string? ReferenceId { get; set; }

        public decimal Quantity { get; set; } // signed: +IN, -OUT
        public decimal? UnitCost { get; set; }
        public string? Remarks { get; set; }

        public int? MovedByUserId { get; set; }
        public User? MovedBy { get; set; }

        public DateTime MovedAt { get; set; } = DateTime.UtcNow;
    }
}
