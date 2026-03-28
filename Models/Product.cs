// Models/Product.cs
using ShopAPI.Models.Identity;


namespace ShopAPI.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Who created this product ──
        public string SellerId { get; set; } = string.Empty;

        // ── Navigation ──
        public AppUser? Seller { get; set; }
        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}