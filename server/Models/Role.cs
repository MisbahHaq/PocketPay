namespace server.Models
{
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;

        //userroles
        public ICollection<UserRole>? UserRoles { get; set; }
    }
}
