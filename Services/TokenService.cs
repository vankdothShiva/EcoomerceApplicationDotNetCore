// Services/TokenService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ShopAPI.Models.Identity;
using ShopAPI.Data;

namespace ShopAPI.Services
{
    public class TokenService
    {
        private readonly IConfiguration _config;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _db;

        public TokenService(
            IConfiguration config,
            UserManager<AppUser> userManager,
            AppDbContext db)
        {
            _config = config;
            _userManager = userManager;
            _db = db;
        }

        // ── Generate JWT Access Token ──
        public async Task<string> GenerateAccessTokenAsync(AppUser user)
        {
            var jwtSettings = _config.GetSection("Jwt");

            // Get roles & claims from DB
            var roles = await _userManager.GetRolesAsync(user);
            var customClaims = await _userManager.GetClaimsAsync(user);

            // Build claims list
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name,           user.UserName!),
                new Claim(ClaimTypes.Email,          user.Email!),
                new Claim("FirstName",               user.FirstName),
                new Claim("LastName",                user.LastName),
                new Claim(JwtRegisteredClaimNames.Jti,
                          Guid.NewGuid().ToString()), // unique token ID
            };

            // Add each role as a claim
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            // Add custom DB claims (Department, etc.)
            claims.AddRange(customClaims);

            // Sign the token
            var key = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(
                                        int.Parse(jwtSettings["ExpiryHours"]!)),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ── Generate Refresh Token ──
        public async Task<RefreshToken> GenerateRefreshTokenAsync(AppUser user)
        {
            // Revoke all old refresh tokens for this user first
            var oldTokens = _db.RefreshTokens
                               .Where(rt => rt.UserId == user.Id && !rt.IsRevoked);
            foreach (var old in oldTokens)
                old.IsRevoked = true;

            // Create new refresh token
            var refreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(
                                RandomNumberGenerator.GetBytes(64)),
                // 64 random bytes → very hard to guess
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(
                                int.Parse(
                                    _config["Jwt:RefreshTokenExpiryDays"]!)),
                CreatedAt = DateTime.UtcNow,
            };

            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync();

            return refreshToken;
        }

        // ── Get UserId from expired Access Token ──
        // Used when refreshing: old token → find user → give new token
        public string? GetUserIdFromExpiredToken(string token)
        {
            var jwtSettings = _config.GetSection("Jwt");

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                                               Encoding.UTF8.GetBytes(
                                                   jwtSettings["Key"]!)),
                // Important: don't reject expired tokens here!
                ValidateLifetime = false
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, parameters, out _);
            return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}