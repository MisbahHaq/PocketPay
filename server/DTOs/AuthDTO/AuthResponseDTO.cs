namespace server.DTOs.AuthDTO
{
    public class AuthResponseDTO
    {
        public string Token { get; set; } = String.Empty;

        public string Username { get; set; } = String.Empty;

        public string Email { get; set; } = String.Empty;

        public DateTime ExpiresIn { get; set; }

        public List<string> Roles = new();

    }

}
