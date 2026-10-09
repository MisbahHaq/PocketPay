namespace server.Models.Inventory
{
    public class Supplier
    {
        public int SupplierId { get; set; }
        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? TaxNo { get; set; }
        public string? Address { get; set; }
        public string? PaymentTerms { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
