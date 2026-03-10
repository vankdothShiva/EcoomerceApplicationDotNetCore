// Models/Identity/DTOs/RegisterDto.cs
namespace ShopAPI.Models.Identity.DTOs
{
    public class RegisterDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? City { get; set; }
        // Role: "Customer" or "Seller" (Admin created by seed only)
        public string Role { get; set; } = "Customer";
    }

   

    

    


   

   
}