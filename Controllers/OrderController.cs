// Controllers/OrderController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopAPI.Data;
using ShopAPI.Models;

using System.Security.Claims;
using WebApplication1.DTOs;

namespace ShopAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly AppDbContext _db;

        public OrderController(AppDbContext db)
        {
            _db = db;
        }

        // ════════════════════════════════════════
        // CUSTOMER ENDPOINTS
        // ════════════════════════════════════════

        // ── PLACE ORDER ──
        [HttpPost]
        [Authorize(Policy = "CustomerOrAdmin")]
        public async Task<IActionResult> PlaceOrder(
            [FromBody] CreateOrderDto dto)
        {
            var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;

            // Validate items list not empty
            if (dto.Items == null || !dto.Items.Any())
                return BadRequest(new { error = "Order must have at least one item." });

            // STEP 1: Build order items + calculate total
            var orderItems = new List<OrderItem>();
            decimal total = 0;

            foreach (var item in dto.Items)
            {
                // Find product
                var product = await _db.Products
                    .FirstOrDefaultAsync(p =>
                        p.Id == item.ProductId && p.IsActive);

                if (product == null)
                    return BadRequest(new
                    {
                        error = $"Product {item.ProductId} not found or inactive."
                    });

                // Check stock
                if (product.Stock < item.Quantity)
                    return BadRequest(new
                    {
                        error = $"Insufficient stock for '{product.Name}'. " +
                                $"Available: {product.Stock}, Requested: {item.Quantity}"
                    });

                // Add order item
                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                    // UnitPrice saved at time of order
                    // so price changes later don't affect old orders
                });

                total += product.Price * item.Quantity;

                // Reduce stock
                product.Stock -= item.Quantity;
            }

            // STEP 2: Create order
            var order = new Order
            {
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                TotalAmount = total,
                ShipAddress = dto.ShipAddress,
                CreatedAt = DateTime.UtcNow,
                OrderItems = orderItems
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetMyOrderById),
                new { id = order.Id },
                new
                {
                    message = "Order placed successfully!",
                    orderId = order.Id,
                    total = order.TotalAmount,
                    status = order.Status.ToString()
                });
        }

        // ── GET MY ORDERS ──
        [HttpGet("my-orders")]
        [Authorize(Policy = "CustomerOrAdmin")]
        public async Task<IActionResult> GetMyOrders()
        {
            var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var orders = await _db.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderResponseDto
                {
                    Id = o.Id,
                    CustomerId = o.CustomerId,
                    Status = o.Status.ToString(),
                    TotalAmount = o.TotalAmount,
                    ShipAddress = o.ShipAddress,
                    CreatedAt = o.CreatedAt,
                    Items = o.OrderItems.Select(oi =>
                        new OrderItemResponseDto
                        {
                            ProductId = oi.ProductId,
                            ProductName = oi.Product!.Name,
                            Quantity = oi.Quantity,
                            UnitPrice = oi.UnitPrice,
                            SubTotal = oi.UnitPrice * oi.Quantity
                        }).ToList()
                })
                .ToListAsync();

            return Ok(new { count = orders.Count, orders });
        }

        // ── GET SINGLE MY ORDER ──
        [HttpGet("my-orders/{id}")]
        [Authorize(Policy = "CustomerOrAdmin")]
        public async Task<IActionResult> GetMyOrderById(int id)
        {
            var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var order = await _db.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o =>
                    o.Id == id && o.CustomerId == customerId);

            if (order == null)
                return NotFound(new { error = "Order not found." });

            return Ok(new OrderResponseDto
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                CustomerName = $"{order.Customer!.FirstName} {order.Customer.LastName}",
                Status = order.Status.ToString(),
                TotalAmount = order.TotalAmount,
                ShipAddress = order.ShipAddress,
                CreatedAt = order.CreatedAt,
                Items = order.OrderItems.Select(oi =>
                    new OrderItemResponseDto
                    {
                        ProductId = oi.ProductId,
                        ProductName = oi.Product!.Name,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        SubTotal = oi.UnitPrice * oi.Quantity
                    }).ToList()
            });
        }

        // ── CANCEL MY ORDER ──
        [HttpPost("my-orders/{id}/cancel")]
        [Authorize(Policy = "CustomerOrAdmin")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var order = await _db.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o =>
                    o.Id == id && o.CustomerId == customerId);

            if (order == null)
                return NotFound(new { error = "Order not found." });

            // Can only cancel Pending orders
            if (order.Status != OrderStatus.Pending)
                return BadRequest(new
                {
                    error = $"Cannot cancel order with status '{order.Status}'. " +
                             "Only Pending orders can be cancelled."
                });

            // Restore stock
            foreach (var item in order.OrderItems)
            {
                if (item.Product != null)
                    item.Product.Stock += item.Quantity;
            }

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Order cancelled. Stock restored." });
        }

        // ════════════════════════════════════════
        // SELLER ENDPOINTS
        // ════════════════════════════════════════

        // ── UPDATE ORDER STATUS (Seller confirms/ships) ──
        [HttpPut("{id}/status")]
        [Authorize(Policy = "SellerOrAdmin")]
        public async Task<IActionResult> UpdateOrderStatus(
            int id, [FromBody] UpdateOrderStatusDto dto)
        {
            var order = await _db.Orders.FindAsync(id);
            if (order == null)
                return NotFound(new { error = "Order not found." });

            // Validate status transition
            var validTransitions = new Dictionary<OrderStatus, List<OrderStatus>>
            {
                { OrderStatus.Pending,   new() { OrderStatus.Confirmed, OrderStatus.Cancelled } },
                { OrderStatus.Confirmed, new() { OrderStatus.Shipped } },
                { OrderStatus.Shipped,   new() { OrderStatus.Delivered } },
                { OrderStatus.Delivered, new() { } },  // final state
                { OrderStatus.Cancelled, new() { } },  // final state
            };

            if (!validTransitions[order.Status].Contains(dto.Status))
                return BadRequest(new
                {
                    error = $"Cannot change status from " +
                            $"'{order.Status}' to '{dto.Status}'.",
                    allowedTransitions = validTransitions[order.Status]
                                            .Select(s => s.ToString())
                });

            order.Status = dto.Status;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = $"Order status updated to '{dto.Status}'.",
                orderId = order.Id,
                newStatus = order.Status.ToString()
            });
        }
    }
}