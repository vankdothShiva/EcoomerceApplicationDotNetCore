// Controllers/MfaController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShopAPI.Models.Identity;
using ShopAPI.Models.Identity.DTOs;
using ShopAPI.Services;
using System.Security.Claims;
using WebApplication1.DTOs;

namespace ShopAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MfaController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly TokenService _tokenService;
        private readonly EmailService _emailService;
        private readonly TotpService _totpService;

        public MfaController(
            UserManager<AppUser> userManager,
            TokenService tokenService,
            EmailService emailService,
            TotpService totpService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _emailService = emailService;
            _totpService = totpService;
        }

        // ════════════════════════════════════════
        // EMAIL OTP — SECTION A
        // ════════════════════════════════════════

        // ── 1. Enable Email OTP MFA ──
        [HttpPost("email-otp/enable")]
        [Authorize]
        public async Task<IActionResult> EnableEmailOtp()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            // Check if already enabled
            if (user.TwoFactorEnabled)
                return BadRequest(new { error = "MFA is already enabled." });

            // Enable TwoFactor on the user
            await _userManager.SetTwoFactorEnabledAsync(user, true);

            return Ok(new
            {
                message = "Email OTP MFA enabled! " +
                          "You will receive a code on every login."
            });
        }

        // ── 2. Disable Email OTP MFA ──
        [HttpPost("email-otp/disable")]
        [Authorize]
        public async Task<IActionResult> DisableEmailOtp()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            if (!user.TwoFactorEnabled)
                return BadRequest(new { error = "MFA is not enabled." });

            // If user has authenticator set up, don't allow disable from here
            if (user.AuthenticatorSetupComplete)
                return BadRequest(new
                {
                    error = "You have Authenticator App MFA active. " +
                            "Please disable it from /api/mfa/authenticator/disable"
                });

            await _userManager.SetTwoFactorEnabledAsync(user, false);

            return Ok(new { message = "Email OTP MFA disabled." });
        }

        // ── 3. Verify Email OTP (called after login when RequiresMfa=true) ──
        [HttpPost("email-otp/verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmailOtp(
            [FromBody] VerifyOtpDto dto)
        {
            // STEP 1: Find user by ID
            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null)
                return BadRequest(new { error = "Invalid request." });

            // STEP 2: Check OTP exists in DB
            if (string.IsNullOrEmpty(user.EmailOtp))
                return BadRequest(new
                {
                    error = "No OTP found. Please login again to get a new code."
                });

            // STEP 3: Check OTP not expired (5 min window)
            if (user.EmailOtpExpiry < DateTime.UtcNow)
            {
                // Clear expired OTP
                user.EmailOtp = null;
                user.EmailOtpExpiry = null;
                await _userManager.UpdateAsync(user);

                return BadRequest(new
                {
                    error = "OTP has expired. Please login again."
                });
            }

            // STEP 4: Check OTP matches
            if (user.EmailOtp != dto.Otp.Trim())
                return BadRequest(new { error = "Invalid OTP code." });

            // STEP 5: Clear OTP — one time use only!
            user.EmailOtp = null;
            user.EmailOtpExpiry = null;
            await _userManager.UpdateAsync(user);

            // STEP 6: Generate JWT tokens — login complete!
            var accessToken = await _tokenService.GenerateAccessTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                Email = user.Email!,
                FullName = $"{user.FirstName} {user.LastName}",
                Roles = roles.ToList(),
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                RequiresMfa = false
            });
        }

        // ── 4. Resend Email OTP ──
        [HttpPost("email-otp/resend")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendEmailOtp([FromBody] string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return BadRequest(new { error = "Invalid request." });

            // Make sure MFA is actually enabled
            if (!user.TwoFactorEnabled)
                return BadRequest(new { error = "MFA is not enabled for this user." });

            // Generate new OTP
            var otp = GenerateOtp();
            user.EmailOtp = otp;
            user.EmailOtpExpiry = DateTime.UtcNow.AddMinutes(5);
            await _userManager.UpdateAsync(user);

            await _emailService.SendOtpEmailAsync(user.Email!, otp);

            return Ok(new
            {
                message = "New OTP sent to your email. Valid for 5 minutes."
            });
        }

        // ════════════════════════════════════════
        // AUTHENTICATOR APP — SECTION B
        // ════════════════════════════════════════

        // ── 5. Get Authenticator Setup (QR Code) ──
        [HttpGet("authenticator/setup")]
        [Authorize]
        public async Task<IActionResult> GetAuthenticatorSetup()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            // Get existing key OR generate new one
            var secretKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(secretKey))
            {
                // First time → generate fresh key
                await _userManager.ResetAuthenticatorKeyAsync(user);
                secretKey = await _userManager.GetAuthenticatorKeyAsync(user);
                // Key is stored in AspNetUserTokens table automatically
            }

            // Build QR URI → convert to image
            var qrUri = _totpService.GenerateQrCodeUri(user.Email!, secretKey!);
            var qrImage = _totpService.GenerateQrCodeImage(qrUri);

            return Ok(new
            {
                secretKey,
                // secretKey → user can type this manually if QR scan fails
                qrCodeImage = $"data:image/png;base64,{qrImage}",
                // paste qrCodeImage value in browser to see the QR code
                instructions = new
                {
                    step1 = "Open Google Authenticator or Microsoft Authenticator",
                    step2 = "Tap '+' → 'Scan QR code'",
                    step3 = "Scan the QR code image above",
                    step4 = "Enter the 6-digit code shown in the app",
                    step5 = "POST to /api/mfa/authenticator/confirm with the code"
                }
            });
        }

        // ── 6. Confirm Authenticator Setup ──
        [HttpPost("authenticator/confirm")]
        [Authorize]
        public async Task<IActionResult> ConfirmAuthenticatorSetup(
            [FromBody] string code)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            // Get the stored secret key
            var secretKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(secretKey))
                return BadRequest(new
                {
                    error = "No authenticator key found. " +
                            "Please call /api/mfa/authenticator/setup first."
                });

            // Verify the code from the app
            var isValid = _totpService.VerifyCode(secretKey, code.Trim());
            if (!isValid)
                return BadRequest(new
                {
                    error = "Invalid code. Make sure your phone time is correct."
                });

            // Mark setup as complete
            user.AuthenticatorSetupComplete = true;
            await _userManager.SetTwoFactorEnabledAsync(user, true);
            await _userManager.UpdateAsync(user);

            // Generate recovery codes (show these ONCE to user)
            var recoveryCodes = await _userManager
                .GenerateNewTwoFactorRecoveryCodesAsync(user, 8);

            return Ok(new
            {
                message = "Authenticator App MFA enabled successfully!",
                recoveryCodes,
                // ⚠️ IMPORTANT: Save these recovery codes!
                // If you lose your phone, use these to login
                warning = "Save these recovery codes somewhere safe. " +
                          "They will NOT be shown again!"
            });
        }

        // ── 7. Verify Authenticator Code (called after login) ──
        [HttpPost("authenticator/verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyAuthenticator(
            [FromBody] VerifyOtpDto dto)
        {
            // STEP 1: Find user
            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null)
                return BadRequest(new { error = "Invalid request." });

            // STEP 2: Make sure authenticator is set up
            if (!user.AuthenticatorSetupComplete)
                return BadRequest(new
                {
                    error = "Authenticator not set up. Use email OTP instead."
                });

            // STEP 3: Get secret key from DB
            var secretKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(secretKey))
                return BadRequest(new { error = "Authenticator key not found." });

            // STEP 4: Verify 6-digit TOTP code
            var isValid = _totpService.VerifyCode(secretKey, dto.Otp.Trim());
            if (!isValid)
                return BadRequest(new
                {
                    error = "Invalid or expired code. Codes refresh every 30 seconds."
                });

            // STEP 5: Generate JWT — login complete!
            var accessToken = await _tokenService.GenerateAccessTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                Email = user.Email!,
                FullName = $"{user.FirstName} {user.LastName}",
                Roles = roles.ToList(),
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                RequiresMfa = false
            });
        }

        // ── 8. Verify Recovery Code ──
        [HttpPost("authenticator/recover")]
        [AllowAnonymous]
        public async Task<IActionResult> UseRecoveryCode(
            [FromBody] VerifyOtpDto dto)
        {
            // Used when user lost their phone
            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null)
                return BadRequest(new { error = "Invalid request." });

            // Identity handles recovery code validation
            var result = await _userManager
                .RedeemTwoFactorRecoveryCodeAsync(user, dto.Otp.Trim());

            if (!result.Succeeded)
                return BadRequest(new
                {
                    error = "Invalid recovery code."
                });

            // Give JWT
            var accessToken = await _tokenService.GenerateAccessTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                Email = user.Email!,
                FullName = $"{user.FirstName} {user.LastName}",
                Roles = roles.ToList(),
                ExpiresAt = DateTime.UtcNow.AddHours(1),
            });
        }

        // ── 9. Disable Authenticator MFA ──
        [HttpPost("authenticator/disable")]
        [Authorize]
        public async Task<IActionResult> DisableAuthenticator()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            if (!user.AuthenticatorSetupComplete)
                return BadRequest(new
                {
                    error = "Authenticator MFA is not enabled."
                });

            // Remove authenticator key from AspNetUserTokens
            await _userManager.ResetAuthenticatorKeyAsync(user);

            // Mark setup as incomplete
            user.AuthenticatorSetupComplete = false;

            // Disable 2FA entirely
            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.UpdateAsync(user);

            return Ok(new { message = "Authenticator MFA disabled." });
        }

        // ── 10. Get MFA Status ──
        [HttpGet("status")]
        [Authorize]
        public async Task<IActionResult> GetMfaStatus()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            return Ok(new
            {
                twoFactorEnabled = user.TwoFactorEnabled,
                authenticatorSetupComplete = user.AuthenticatorSetupComplete,
                mfaType = user.AuthenticatorSetupComplete
                              ? "AuthenticatorApp"
                              : user.TwoFactorEnabled
                                  ? "EmailOTP"
                                  : "None"
            });
        }

        // ════════════════════════════════════════
        // PRIVATE HELPER
        // ════════════════════════════════════════
        private static string GenerateOtp() =>
            new Random().Next(100000, 999999).ToString();
    }
}