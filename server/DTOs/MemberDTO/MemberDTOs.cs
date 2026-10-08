namespace server.DTOs.MemberDTO
{
    public class MemberResponseDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class AssignableMemberDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }

    public class UpdateMemberDTO
    {
        public int? RoleId { get; set; }
        public string? Status { get; set; }
    }
}
