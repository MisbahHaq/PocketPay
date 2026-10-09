namespace server.DTOs.Inventory
{
    public class UnitDTO
    {
        public int UnitId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
