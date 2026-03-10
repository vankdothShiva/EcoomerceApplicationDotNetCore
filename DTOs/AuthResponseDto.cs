namespace WebApplication1.DTOs
{
    public class AuthResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public DateTime ExpiresAt { get; set; }
        public bool RequiresMfa { get; set; } = false;
        public string? UserId { get; set; } // only when MFA needed
    }

}
