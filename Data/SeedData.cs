// Data/SeedData.cs
using Microsoft.AspNetCore.Identity;
using ShopAPI.Models.Identity;

namespace ShopAPI.Data
{
    public static class SeedData
    {
        public static async Task SeedAsync(
            RoleManager<IdentityRole> roleManager,
            UserManager<AppUser> userManager,
            IConfiguration config)
        {
            // ── STEP 1: Create Roles ──
            var roles = new[] { "Admin", "Seller", "Customer" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                    Console.WriteLine($"✅ Role created: {role}");
                }
            }

            // ── STEP 2: Create Admin User ──
            var adminEmail = config["AdminSeed:Email"]!;
            var adminPassword = config["AdminSeed:Password"]!;

            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin == null)
            {
                var admin = new AppUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "Super",
                    LastName = "Admin",
                    EmailConfirmed = true,   // Admin doesn't need email confirm
                    IsActive = true,
                };

                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                    Console.WriteLine($"✅ Admin user created: {adminEmail}");
                }
                else
                {
                    var errs = string.Join(", ",
                        result.Errors.Select(e => e.Description));
                    Console.WriteLine($"❌ Admin creation failed: {errs}");
                }
            }

            // ── STEP 3: Create Sample Seller ──
            var sellerEmail = "seller@shopapi.com";
            if (await userManager.FindByEmailAsync(sellerEmail) == null)
            {
                var seller = new AppUser
                {
                    UserName = sellerEmail,
                    Email = sellerEmail,
                    FirstName = "Sample",
                    LastName = "Seller",
                    EmailConfirmed = true,
                    IsActive = true,
                };
                var result = await userManager.CreateAsync(seller, "Seller@123456");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(seller, "Seller");
            }

            // ── STEP 4: Create Sample Customer ──
            var customerEmail = "customer@shopapi.com";
            if (await userManager.FindByEmailAsync(customerEmail) == null)
            {
                var customer = new AppUser
                {
                    UserName = customerEmail,
                    Email = customerEmail,
                    FirstName = "Sample",
                    LastName = "Customer",
                    EmailConfirmed = true,
                    IsActive = true,
                };
                var result = await userManager.CreateAsync(customer, "Customer@123456");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(customer, "Customer");
            }

            Console.WriteLine("✅ Seed data complete!");
        }
    }
}