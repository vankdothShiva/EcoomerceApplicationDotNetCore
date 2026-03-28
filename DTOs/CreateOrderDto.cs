namespace WebApplication1.DTOs
{
    public class CreateOrderDto
    {
        public string? ShipAddress { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }
}
