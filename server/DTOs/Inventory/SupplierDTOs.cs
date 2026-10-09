namespace server.DTOs.Inventory
{
    public class SupplierDTO
    {
        public int SupplierId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public bool IsActive { get; set; }
    }

    public class SupplierUpsertDTO
    {
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? TaxNo { get; set; }
        public string? Address { get; set; }
        public string? PaymentTerms { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
