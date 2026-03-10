// Data/AppDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShopAPI.Models;
using ShopAPI.Models.Identity;

namespace ShopAPI.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        // ── Our tables ──
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // MUST call this for Identity tables

            // ── Product config ──
            builder.Entity<Product>(e => {
                e.Property(p => p.Price)
                 .HasColumnType("decimal(18,2)"); // 2 decimal places
                e.HasOne(p => p.Seller)
                 .WithMany(u => u.Products)
                 .HasForeignKey(p => p.SellerId)
                 .OnDelete(DeleteBehavior.Restrict);
                // Restrict = can't delete seller if they have products
            });

            // ── Order config ──
            builder.Entity<Order>(e => {
                e.Property(o => o.TotalAmount)
                 .HasColumnType("decimal(18,2)");
                e.HasOne(o => o.Customer)
                 .WithMany(u => u.Orders)
                 .HasForeignKey(o => o.CustomerId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── OrderItem config ──
            builder.Entity<OrderItem>(e => {
                e.Property(oi => oi.UnitPrice)
                 .HasColumnType("decimal(18,2)");
                e.HasOne(oi => oi.Order)
                 .WithMany(o => o.OrderItems)
                 .HasForeignKey(oi => oi.OrderId)
                 .OnDelete(DeleteBehavior.Cascade);
                // Cascade = delete order → delete its items too
                e.HasOne(oi => oi.Product)
                 .WithMany(p => p.OrderItems)
                 .HasForeignKey(oi => oi.ProductId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── RefreshToken config ──
            builder.Entity<RefreshToken>(e => {
                e.HasOne(rt => rt.User)
                 .WithMany(u => u.RefreshTokens)
                 .HasForeignKey(rt => rt.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}