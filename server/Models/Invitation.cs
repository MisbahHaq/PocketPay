namespace server.Models
{
    public static class InvitationStatus
    {
        public const string Pending = "Pending";
        public const string Accepted = "Accepted";
        public const string Revoked = "Revoked";
        public const string Expired = "Expired";
    }

    public class Invitation
    {
        public int InvitationId { get; set; }

        public int OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        public string Email { get; set; } = string.Empty;

        public int RoleId { get; set; }
        public Role? Role { get; set; }

        public int InvitedById { get; set; }
        public User? InvitedBy { get; set; }

        public string Token { get; set; } = string.Empty;
        public string Status { get; set; } = InvitationStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public int? AcceptedByUserId { get; set; }
    }
}
