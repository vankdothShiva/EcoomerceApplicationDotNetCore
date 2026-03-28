// Controllers/ProductController.cs
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
    public class ProductController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ProductController(AppDbContext db)
        {
            _db = db;
        }

        // ════════════════════════════════════════
        // PUBLIC ENDPOINTS — No login needed
        // ════════════════════════════════════════

        // ── GET all active products (with search & filter) ──
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllProducts(
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            // Start with all active products
            var query = _db.Products
                           .Include(p => p.Seller)
                           .Where(p => p.IsActive)
                           .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(search))
                query = query.Where(p =>
                    p.Name.Contains(search) ||
                    p.Description.Contains(search));

            if (!string.IsNullOrEmpty(category))
                query = query.Where(p =>
                    p.Category.ToLower() == category.ToLower());

            if (minPrice.HasValue)
                query = query.Where(p => p.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.Price <= maxPrice.Value);

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply pagination
            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)  // skip previous pages
                .Take(pageSize)                // take only this page
                .Select(p => new ProductResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Stock = p.Stock,
                    Category = p.Category,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    SellerName = $"{p.Seller!.FirstName} {p.Seller.LastName}",
                    SellerId = p.SellerId
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(
                                 (double)totalCount / pageSize),
                products
            });
        }

        // ── GET single product by ID ──
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProduct(int id)
        {
            var product = await _db.Products
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (product == null)
                return NotFound(new { error = $"Product {id} not found." });

            return Ok(new ProductResponseDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                Category = product.Category,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt,
                SellerName = $"{product.Seller!.FirstName} {product.Seller.LastName}",
                SellerId = product.SellerId
            });
        }

        // ── GET all categories ──
        [HttpGet("categories")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _db.Products
                .Where(p => p.IsActive)
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(new { categories });
        }

        // ════════════════════════════════════════
        // SELLER ENDPOINTS — Seller or Admin only
        // ════════════════════════════════════════

        // ── CREATE product ──
        [HttpPost]
        [Authorize(Policy = "SellerOrAdmin")]
        public async Task<IActionResult> CreateProduct(
            [FromBody] CreateProductDto dto)
        {
            var sellerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;

            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                Stock = dto.Stock,
                Category = dto.Category,
                SellerId = sellerId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetProduct),
                new { id = product.Id },
                new { message = "Product created!", productId = product.Id });
        }

        // ── UPDATE product ──
        [HttpPut("{id}")]
        [Authorize(Policy = "SellerOrAdmin")]
        public async Task<IActionResult> UpdateProduct(
            int id, [FromBody] UpdateProductDto dto)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null)
                return NotFound(new { error = "Product not found." });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("Admin");

            // Seller can only update THEIR OWN products
            if (!isAdmin && product.SellerId != userId)
                return StatusCode(403, new
                {
                    error = "You can only update your own products."
                });

            // Update fields
            product.Name = dto.Name;
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.Stock = dto.Stock;
            product.Category = dto.Category;
            product.IsActive = dto.IsActive;

            await _db.SaveChangesAsync();

            return Ok(new { message = "Product updated successfully!" });
        }

        // ── GET my products (Seller only) ──
        [HttpGet("my-products")]
        [Authorize(Policy = "SellerOrAdmin")]
        public async Task<IActionResult> GetMyProducts()
        {
            var sellerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var products = await _db.Products
                .Where(p => p.SellerId == sellerId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProductResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Stock = p.Stock,
                    Category = p.Category,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    SellerId = p.SellerId
                })
                .ToListAsync();

            return Ok(new { count = products.Count, products });
        }

        // ── DELETE product (Admin only) ──
        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null)
                return NotFound(new { error = "Product not found." });

            // Soft delete — just deactivate
            product.IsActive = false;
            await _db.SaveChangesAsync();

            return Ok(new { message = $"Product {id} deactivated." });
        }
    }
}