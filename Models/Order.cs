// Models/Order.cs
using ShopAPI.Models.Identity;


namespace ShopAPI.Models
{
    public enum OrderStatus
    {
        Pending,    // just placed
        Confirmed,  // seller confirmed
        Shipped,    // on the way
        Delivered,  // received
        Cancelled   // cancelled
    }

    public class Order
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public decimal TotalAmount { get; set; }
        public string? ShipAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // ── Navigation ──
        public AppUser? Customer { get; set; }
        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}