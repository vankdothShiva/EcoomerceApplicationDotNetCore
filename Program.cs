using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShopAPI.Data;
using ShopAPI.Models.Identity;
using ShopAPI.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ════════════════════════════════════════
// 1. DATABASE
// ════════════════════════════════════════
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ════════════════════════════════════════
// 2. IDENTITY
// ════════════════════════════════════════
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    // Password rules
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;

    // Lockout after 5 wrong attempts
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    // Unique email required
    options.User.RequireUniqueEmail = true;

    // Email must be confirmed before login
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ════════════════════════════════════════
// 3. JWT AUTHENTICATION
// ════════════════════════════════════════
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };

    // Custom error messages
    options.Events = new JwtBearerEvents
    {
        OnChallenge = ctx =>
        {
            ctx.HandleResponse();
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync(
                "{\"error\":\"Not authenticated. Please login.\"}");
        },
        OnForbidden = ctx =>
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync(
                "{\"error\":\"Access denied. Insufficient permissions.\"}");
        }
    };
})
.AddFacebook(options =>
{
    options.AppId = builder.Configuration["Facebook:AppId"]!;
    options.AppSecret = builder.Configuration["Facebook:AppSecret"]!;
    options.CallbackPath = "/api/externalauth/facebook-callback";
});

// ════════════════════════════════════════
// 4. AUTHORIZATION POLICIES
// ════════════════════════════════════════
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("SellerOrAdmin", p => p.RequireRole("Seller", "Admin"));
    options.AddPolicy("CustomerOrAdmin", p => p.RequireRole("Customer", "Admin"));
    options.AddPolicy("AnyRole", p => p.RequireRole("Admin", "Seller", "Customer"));
});

// ════════════════════════════════════════
// 5. SWAGGER WITH JWT SUPPORT
// ════════════════════════════════════════
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShopAPI",
        Version = "v1",
        Description = "E-Commerce API with JWT, MFA & Social Login"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter EXACTLY like this:\n\n" +
                      "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\n\n" +
                      "⚠️ Must include 'Bearer ' prefix with a space!"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ════════════════════════════════════════
// 6. SERVICES
// ════════════════════════════════════════
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<TotpService>();
builder.Services.AddHttpClient<FacebookAuthService>();
// ↑ typed HttpClient — injects HttpClient directly into FacebookAuthService
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ════════════════════════════════════════
// BUILD APP
// ════════════════════════════════════════
var app = builder.Build();

// ── Run Seed Data on startup ──
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
                         .GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider
                         .GetRequiredService<UserManager<AppUser>>();
    var config = scope.ServiceProvider
                         .GetRequiredService<IConfiguration>();

    await SeedData.SeedAsync(roleManager, userManager, config);
}

// ── Middleware Pipeline ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    // ✅ FIXED: Full SwaggerUI options with persistAuthorization
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ShopAPI v1");

        // Token stays after page refresh
        options.ConfigObject.AdditionalItems["persistAuthorization"] = true;

        // Show how long each request takes
        options.DisplayRequestDuration();

        // All endpoint groups collapsed by default (cleaner)
        options.DocExpansion(
            Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();  // 1st: Who are you?
app.UseAuthorization();   // 2nd: What can you do?
app.MapControllers();
app.Run();