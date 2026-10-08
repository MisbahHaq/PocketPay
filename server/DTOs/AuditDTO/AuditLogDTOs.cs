namespace server.DTOs.AuditDTO
{
    public class AuditLogResponseDTO
    {
        public int AuditLogId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? ActorName { get; set; }
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
