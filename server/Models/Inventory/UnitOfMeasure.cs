namespace server.Models.Inventory
{
    public class UnitOfMeasure
    {
        public int UnitId { get; set; }
        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Product>? Products { get; set; }
    }
}
