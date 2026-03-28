// Controllers/AdminController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopAPI.Data;
using ShopAPI.Models;
using ShopAPI.Models.Identity;

namespace ShopAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminOnly")]  // ALL endpoints = Admin only
    public class AdminController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _db;

        public AdminController(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }

        // ── DASHBOARD STATS ──
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var totalUsers = await _userManager.Users.CountAsync();
            var totalProducts = await _db.Products.CountAsync();
            var totalOrders = await _db.Orders.CountAsync();
            var totalRevenue = await _db.Orders
                .Where(o => o.Status != OrderStatus.Cancelled)
                .SumAsync(o => o.TotalAmount);

            var recentOrders = await _db.Orders
                .Include(o => o.Customer)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .Select(o => new {
                    o.Id,
                    Customer = $"{o.Customer!.FirstName} {o.Customer.LastName}",
                    o.TotalAmount,
                    Status = o.Status.ToString(),
                    o.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                stats = new
                {
                    totalUsers,
                    totalProducts,
                    totalOrders,
                    totalRevenue
                },
                recentOrders
            });
        }

        // ── GET ALL USERS ──
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var totalCount = await _userManager.Users.CountAsync();

            var users = await _userManager.Users
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new List<object>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new
                {
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.EmailConfirmed,
                    user.IsActive,
                    user.TwoFactorEnabled,
                    user.CreatedAt,
                    Roles = roles
                });
            }

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(
                                 (double)totalCount / pageSize),
                users = result
            });
        }

        // ── GET SINGLE USER ──
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { error = "User not found." });

            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);
            var orders = await _db.Orders
                .Where(o => o.CustomerId == id)
                .CountAsync();

            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.Address,
                user.City,
                user.EmailConfirmed,
                user.IsActive,
                user.TwoFactorEnabled,
                user.CreatedAt,
                Roles = roles,
                Claims = claims.Select(c => new { c.Type, c.Value }),
                OrderCount = orders
            });
        }

        // ── ASSIGN ROLE ──
        [HttpPost("users/{id}/assign-role")]
        public async Task<IActionResult> AssignRole(
            string id, [FromBody] string role)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { error = "User not found." });

            if (!await _roleManager.RoleExistsAsync(role))
                return BadRequest(new
                {
                    error = $"Role '{role}' does not exist.",
                    available = new[] { "Admin", "Seller", "Customer" }
                });

            // Remove all existing roles first
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            // Assign new role
            await _userManager.AddToRoleAsync(user, role);

            return Ok(new
            {
                message = $"Role '{role}' assigned to {user.Email}."
            });
        }

        // ── ACTIVATE / DEACTIVATE USER ──
        [HttpPut("users/{id}/toggle-active")]
        public async Task<IActionResult> ToggleUserActive(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { error = "User not found." });

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            return Ok(new
            {
                message = user.IsActive
                               ? $"{user.Email} activated."
                               : $"{user.Email} deactivated.",
                isActive = user.IsActive
            });
        }

        // ── GET ALL ORDERS ──
        [HttpGet("orders")]
        public async Task<IActionResult> GetAllOrders(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = _db.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
                query = query.Where(o => o.Status == orderStatus);

            var totalCount = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new {
                    o.Id,
                    Customer = $"{o.Customer!.FirstName} {o.Customer.LastName}",
                    o.CustomerId,
                    Status = o.Status.ToString(),
                    o.TotalAmount,
                    o.ShipAddress,
                    o.CreatedAt,
                    ItemCount = o.OrderItems.Count
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(
                                 (double)totalCount / pageSize),
                orders
            });
        }

        // ── DELETE PRODUCT ──
        [HttpDelete("products/{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null)
                return NotFound(new { error = "Product not found." });

            product.IsActive = false;
            await _db.SaveChangesAsync();

            return Ok(new { message = $"Product '{product.Name}' deactivated." });
        }
    }
}