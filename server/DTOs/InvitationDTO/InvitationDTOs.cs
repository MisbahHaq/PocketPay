namespace server.DTOs.InvitationDTO
{
    public class CreateInvitationDTO
    {
        public string Email { get; set; } = string.Empty;
        public int RoleId { get; set; }
    }

    public class InvitationResponseDTO
    {
        public int InvitationId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string InvitedBy { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class InvitationLookupDTO
    {
        public string Email { get; set; } = string.Empty;
        public string OrganizationName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool HasAccount { get; set; }
    }

    public class AcceptInvitationDTO
    {
        public string Token { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Password { get; set; }
    }

    public class InvitationCreatedDTO
    {
        public InvitationResponseDTO Invitation { get; set; } = new();
        public string AcceptUrl { get; set; } = string.Empty;
    }
}
