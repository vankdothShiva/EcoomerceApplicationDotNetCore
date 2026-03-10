namespace WebApplication1.DTOs
{
    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        // ConfirmPassword must match NewPassword → validate in controller
    }
}
