// Controllers/AuthController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopAPI.Data;
using ShopAPI.Models.Identity;
using ShopAPI.Models.Identity.DTOs;
using ShopAPI.Services;
using System.Security.Claims;
using WebApplication1.DTOs;

namespace ShopAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly TokenService _tokenService;
        private readonly EmailService _emailService;
        private readonly AppDbContext _db;

        public AuthController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            TokenService tokenService,
            EmailService emailService,
            AppDbContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _tokenService = tokenService;
            _emailService = emailService;
            _db = db;
        }

        // ════════════════════════════════════════
        // 1. REGISTER
        // ════════════════════════════════════════
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            // STEP 1: Validate role — only Customer or Seller allowed
            var allowedRoles = new[] { "Customer", "Seller" };
            if (!allowedRoles.Contains(dto.Role))
                return BadRequest(new { error = "Role must be Customer or Seller" });

            // STEP 2: Build user object
            var user = new AppUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Address = dto.Address,
                City = dto.City,
                IsActive = true,
            };

            // STEP 3: Create user with hashed password
            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

            // STEP 4: Assign role
            await _userManager.AddToRoleAsync(user, dto.Role);

            // STEP 5: Add default claims
            await _userManager.AddClaimAsync(user,
                new Claim("FullName", $"{dto.FirstName} {dto.LastName}"));

            // STEP 6: Generate email confirmation token
            var token = await _userManager
                                  .GenerateEmailConfirmationTokenAsync(user);
            var confirmLink = Url.Action(
                action: "ConfirmEmail",
                controller: "Auth",
                values: new { userId = user.Id, token },
                protocol: Request.Scheme);
            // Builds: https://localhost:5001/api/auth/confirm-email?userId=...&token=...

            // STEP 7: Send confirmation email
            try
            {
                await _emailService.SendConfirmationEmailAsync(user.Email!, confirmLink!);
            }
            catch (Exception ex)
            {
                // User was created successfully even if email fails
                // Log it and inform user
                Console.WriteLine($"❌ Email send failed: {ex.Message}");

                return Ok(new
                {
                    message = "Registration successful but confirmation email failed. " +
                              $"Please contact support or try resending. UserId: {user.Id}",
                    userId = user.Id,     // ← give userId so they can resend later
                    emailSent = false
                });
            }

            return Ok(new
            {
                message = $"Registration successful! " +
                            $"Please check {dto.Email} to confirm your account.",
                emailSent = true
            });
        }

        // ════════════════════════════════════════
        // 2. CONFIRM EMAIL
        // ════════════════════════════════════════
        [HttpGet("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(
            string userId, string token)
        {
            // STEP 1: Find user
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return BadRequest(new { error = "Invalid confirmation link." });

            // STEP 2: Confirm email using token
            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
                return BadRequest(new
                {
                    error = "Confirmation failed. Link may be expired."
                });

            // STEP 3: Send welcome email
            await _emailService.SendWelcomeEmailAsync(
                user.Email!, user.FirstName);

            return Ok(new
            {
                message = "Email confirmed successfully! You can now login."
            });
        }

        // ════════════════════════════════════════
        // 3. LOGIN
        // ════════════════════════════════════════
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            // STEP 1: Find user by email
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Unauthorized(new { error = "Invalid email or password." });

            // STEP 2: Check account is active
            if (!user.IsActive)
                return Unauthorized(new
                {
                    error = "Account is deactivated. Contact support."
                });

            // STEP 3: Email confirmed?
            if (!user.EmailConfirmed)
                return Unauthorized(new
                {
                    error = "Please confirm your email before logging in."
                });

            // STEP 4: Validate password + handle lockout
            var result = await _signInManager.CheckPasswordSignInAsync(
                user, dto.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
                return Unauthorized(new
                {
                    error = "Account locked due to too many failed attempts. " +
                            "Try again in 15 minutes."
                });

            if (!result.Succeeded)
                return Unauthorized(new { error = "Invalid email or password." });

            // STEP 5: MFA enabled? → don't give token yet
            // Replace the MFA check block (STEP 5) with this:

            // STEP 5: MFA enabled?
            if (user.TwoFactorEnabled)
            {
                // Which MFA type does user have?
                if (user.AuthenticatorSetupComplete)
                {
                    // Authenticator App → no email needed, user opens app
                    return Ok(new AuthResponseDto
                    {
                        RequiresMfa = true,
                        UserId = user.Id,
                        // Tell frontend which MFA type to show
                    });
                }
                else
                {
                    // Email OTP → generate and send code
                    var otp = new Random().Next(100000, 999999).ToString();
                    user.EmailOtp = otp;
                    user.EmailOtpExpiry = DateTime.UtcNow.AddMinutes(5);
                    await _userManager.UpdateAsync(user);
                    await _emailService.SendOtpEmailAsync(user.Email!, otp);

                    return Ok(new AuthResponseDto
                    {
                        RequiresMfa = true,
                        UserId = user.Id,
                    });
                }
            }



            // STEP 6: No MFA → generate tokens directly
            return Ok(await BuildAuthResponseAsync(user));
        }

        // ════════════════════════════════════════
        // 4. REFRESH TOKEN
        // ════════════════════════════════════════
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken(
            [FromBody] RefreshTokenDto dto)
        {
            // STEP 1: Get userId from the EXPIRED access token
            var userId = _tokenService.GetUserIdFromExpiredToken(dto.AccessToken);
            if (userId == null)
                return Unauthorized(new { error = "Invalid access token." });

            // STEP 2: Find the refresh token in DB
            var refreshToken = await _db.RefreshTokens
                .FirstOrDefaultAsync(rt =>
                    rt.Token == dto.RefreshToken &&
                    rt.UserId == userId);

            if (refreshToken == null || !refreshToken.IsActive)
                return Unauthorized(new
                {
                    error = "Invalid or expired refresh token. Please login again."
                });

            // STEP 3: Find user
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized(new { error = "User not found." });

            // STEP 4: Revoke old refresh token + generate new pair
            refreshToken.IsRevoked = true;
            await _db.SaveChangesAsync();

            return Ok(await BuildAuthResponseAsync(user));
        }

        // ════════════════════════════════════════
        // 5. LOGOUT
        // ════════════════════════════════════════
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout(
            [FromBody] string refreshToken)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Revoke the refresh token in DB
            var token = await _db.RefreshTokens
                .FirstOrDefaultAsync(rt =>
                    rt.Token == refreshToken &&
                    rt.UserId == userId);

            if (token != null)
            {
                token.IsRevoked = true;
                await _db.SaveChangesAsync();
            }

            // JWT access token → client must delete it on their side
            return Ok(new
            {
                message = "Logged out successfully. Please delete your access token."
            });
        }

        // ════════════════════════════════════════
        // 6. FORGOT PASSWORD
        // ════════════════════════════════════════
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

            // Always return OK even if email not found
            // (prevents email enumeration attacks)
            if (user == null || !user.EmailConfirmed)
                return Ok(new
                {
                    message = "If that email exists, a reset link has been sent."
                });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action(
                "ResetPassword", "Auth",
                new { userId = user.Id, token },
                Request.Scheme);

            await _emailService.SendPasswordResetEmailAsync(user.Email!, resetLink!);

            return Ok(new
            {
                message = "If that email exists, a reset link has been sent."
            });
        }

        // ════════════════════════════════════════
        // 7. RESET PASSWORD
        // ════════════════════════════════════════
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordDto dto)
        {
            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null)
                return BadRequest(new { error = "Invalid request." });

            var result = await _userManager.ResetPasswordAsync(
                user, dto.Token, dto.NewPassword);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

            // Revoke all refresh tokens on password reset (security!)
            var tokens = _db.RefreshTokens.Where(rt => rt.UserId == user.Id);
            foreach (var t in tokens)
                t.IsRevoked = true;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Password reset successful! Please login." });
        }

        // ════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════

        // Build full auth response with both tokens
        private async Task<AuthResponseDto> BuildAuthResponseAsync(AppUser user)
        {
            var accessToken = await _tokenService.GenerateAccessTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                Email = user.Email!,
                FullName = $"{user.FirstName} {user.LastName}",
                Roles = roles.ToList(),
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                RequiresMfa = false
            };
        }

        [HttpPost("resend-confirmation")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendConfirmationEmail(
    [FromBody] string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

            // Don't reveal if email exists or not
            if (user == null || user.EmailConfirmed)
                return Ok(new
                {
                    message = "If that email exists and is unconfirmed, " +
                              "a new link has been sent."
                });

            var token = await _userManager
                                  .GenerateEmailConfirmationTokenAsync(user);
            var confirmLink = Url.Action(
                "ConfirmEmail", "Auth",
                new { userId = user.Id, token },
                Request.Scheme);

            try
            {
                await _emailService.SendConfirmationEmailAsync(user.Email!, confirmLink!);
                return Ok(new { message = "Confirmation email resent successfully!" });
            }
            catch
            {
                return StatusCode(500, new
                {
                    error = "Failed to send email. Check your Gmail App Password settings."
                });
            }
        }

        // Generate 6-digit OTP
        private static string GenerateOtp() =>
            new Random().Next(100000, 999999).ToString();
    }
}