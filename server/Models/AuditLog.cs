namespace server.Models
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }

        public int? OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        public int? ActorUserId { get; set; }
        public User? Actor { get; set; }

        public string Action { get; set; } = string.Empty;
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
