namespace server.DTOs.Inventory
{
    public class WarehouseDTO
    {
        public int WarehouseId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Location { get; set; }
        public bool IsActive { get; set; }
    }
}
