// Models/Identity/AppUser.cs
using Microsoft.AspNetCore.Identity;


namespace ShopAPI.Models.Identity
{
    public class AppUser : IdentityUser
    {
        // ── Personal Info ──
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? ProfilePic { get; set; } // URL to picture

        // ── Account Info ──
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // ── MFA: Email OTP ──
        public string? EmailOtp { get; set; }
        public DateTime? EmailOtpExpiry { get; set; }

        // ── MFA: Authenticator App ──
        public bool AuthenticatorSetupComplete { get; set; } = false;

        // ── Social Login ──
        public string? LoginProvider { get; set; } // "Facebook","Google",null

        // ── Navigation ──
        public ICollection<Product> Products { get; set; } // Seller's products
            = new List<Product>();
        public ICollection<Order> Orders { get; set; } // Customer's orders
            = new List<Order>();
        public ICollection<RefreshToken> RefreshTokens { get; set; }
            = new List<RefreshToken>();
    }
}