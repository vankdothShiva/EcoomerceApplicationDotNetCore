using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
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
    public class ExternalAuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly TokenService _tokenService;
        private readonly FacebookAuthService _facebookService;

        public ExternalAuthController(
            UserManager<AppUser> userManager,
            TokenService tokenService,
            FacebookAuthService facebookService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _facebookService = facebookService;
        }

        // ════════════════════════════════════════
        // APPROACH A — Server Side (Browser Flow)
        // ════════════════════════════════════════

        // ── STEP 1: Redirect to Facebook login page ──
        [HttpGet("login-facebook")]
        [AllowAnonymous]
        public IActionResult LoginWithFacebook()
        {
            // Build the redirect back to our callback URL
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action(
                    "FacebookCallback", "ExternalAuth",
                    null, Request.Scheme)
                // After Facebook auth → redirects to FacebookCallback below
            };

            // Challenge = tell ASP.NET → redirect user to Facebook login
            return Challenge(properties, FacebookDefaults.AuthenticationScheme);
        }

        // ── STEP 2: Facebook redirects back here ──
        [HttpGet("facebook-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> FacebookCallback()
        {
            // STEP A: Read Facebook authentication result
            var authResult = await HttpContext.AuthenticateAsync(
                                 FacebookDefaults.AuthenticationScheme);

            if (!authResult.Succeeded)
                return Unauthorized(new
                {
                    error = "Facebook login failed.",
                    details = authResult.Failure?.Message
                });

            // STEP B: Extract info from Facebook claims
            var facebookId = authResult.Principal
                                 .FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var name = authResult.Principal
                                 .FindFirst(ClaimTypes.Name)?.Value;
            var email = authResult.Principal
                                 .FindFirst(ClaimTypes.Email)?.Value;

            // Facebook email can be null if user denied email permission
            if (string.IsNullOrEmpty(email))
                return BadRequest(new
                {
                    error = "Email not provided by Facebook. " +
                            "Please grant email permission and try again."
                });

            // STEP C: Find or create user in our DB
            var user = await FindOrCreateUserAsync(
                           facebookId!, name!, email, "Facebook");

            if (user == null)
                return StatusCode(500, new
                {
                    error = "Failed to create user account."
                });

            // STEP D: Generate our JWT tokens
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

        // ════════════════════════════════════════
        // APPROACH B — Mobile / SPA (Token Flow)
        // ════════════════════════════════════════

        // Frontend gets Facebook token using FB SDK → sends to us
        [HttpPost("facebook-token")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginWithFacebookToken(
            [FromBody] ExternalAuthDto dto)
        {
            if (string.IsNullOrEmpty(dto.AccessToken))
                return BadRequest(new { error = "Access token is required." });

            // STEP 1: Verify token with Facebook API
            var fbUser = await _facebookService.ValidateTokenAsync(dto.AccessToken);

            if (fbUser == null)
                return Unauthorized(new
                {
                    error = "Invalid or expired Facebook token."
                });

            // Facebook email can be null
            if (string.IsNullOrEmpty(fbUser.Email))
                return BadRequest(new
                {
                    error = "Email not available from Facebook. " +
                            "Please grant email permission."
                });

            // STEP 2: Find or create user
            var user = await FindOrCreateUserAsync(
                           fbUser.Id, fbUser.Name, fbUser.Email, "Facebook");

            if (user == null)
                return StatusCode(500, new
                {
                    error = "Failed to create user account."
                });

            // STEP 3: Give our JWT
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

        // ════════════════════════════════════════
        // LINKED ACCOUNTS — View & Unlink
        // ════════════════════════════════════════

        // ── Get all social logins linked to my account ──
        [HttpGet("linked-accounts")]
        [Authorize]
        public async Task<IActionResult> GetLinkedAccounts()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            // Get all external logins from AspNetUserLogins table
            var logins = await _userManager.GetLoginsAsync(user);

            return Ok(new
            {
                linkedAccounts = logins.Select(l => new {
                    provider = l.LoginProvider,   // "Facebook"
                    providerKey = l.ProviderKey,     // Facebook user ID
                    providerDisplay = l.ProviderDisplayName
                }),
                hasPassword = await _userManager.HasPasswordAsync(user)
                // false = social-only user (no password set)
            });
        }

        // ── Unlink a social account ──
        [HttpDelete("unlink/{provider}")]
        [Authorize]
        public async Task<IActionResult> UnlinkAccount(string provider)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            // Safety check — must have password OR another login before unlinking
            var logins = await _userManager.GetLoginsAsync(user);
            var hasPassword = await _userManager.HasPasswordAsync(user);

            if (logins.Count == 1 && !hasPassword)
                return BadRequest(new
                {
                    error = "Cannot unlink your only login method. " +
                            "Please set a password first."
                });

            // Find the login to remove
            var login = logins.FirstOrDefault(l =>
                l.LoginProvider.Equals(provider,
                    StringComparison.OrdinalIgnoreCase));

            if (login == null)
                return BadRequest(new
                {
                    error = $"{provider} account is not linked."
                });

            var result = await _userManager.RemoveLoginAsync(
                user, login.LoginProvider, login.ProviderKey);

            if (!result.Succeeded)
                return BadRequest(new
                {
                    errors = result.Errors.Select(e => e.Description)
                });

            // Clear LoginProvider field on user
            user.LoginProvider = null;
            await _userManager.UpdateAsync(user);

            return Ok(new
            {
                message = $"{provider} account unlinked successfully."
            });
        }

        // ════════════════════════════════════════
        // PRIVATE HELPER
        // ════════════════════════════════════════
        private async Task<AppUser?> FindOrCreateUserAsync(
            string providerId,
            string name,
            string email,
            string provider)
        {
            // ── CHECK 1: Existing social login in AspNetUserLogins ──
            var existingLogin = await _userManager
                                    .FindByLoginAsync(provider, providerId);
            if (existingLogin != null)
            {
                Console.WriteLine($"✅ Returning {provider} user: {email}");
                return existingLogin; // returning user → done!
            }

            // ── CHECK 2: User exists with same email ──
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                // ── NEW USER: Create account ──
                var nameParts = name.Split(' ',
                    StringSplitOptions.RemoveEmptyEntries);

                user = new AppUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = nameParts.FirstOrDefault() ?? name,
                    LastName = nameParts.Length > 1
                                         ? string.Join(" ", nameParts.Skip(1))
                                         : "",
                    EmailConfirmed = true,
                    // ↑ Trust Facebook's email — no confirmation needed
                    IsActive = true,
                    LoginProvider = provider,
                };

                // Create without password — social login only
                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    Console.WriteLine($"❌ Failed to create user: " +
                        string.Join(", ", createResult.Errors
                            .Select(e => e.Description)));
                    return null;
                }

                // Assign default Customer role
                await _userManager.AddToRoleAsync(user, "Customer");

                // Add claims
                await _userManager.AddClaimAsync(user,
                    new Claim("FullName", $"{user.FirstName} {user.LastName}"));
                await _userManager.AddClaimAsync(user,
                    new Claim("LoginProvider", provider));

                Console.WriteLine($"✅ New {provider} user created: {email}");
            }
            else
            {
                // ── EXISTING USER: Link social login to their account ──
                Console.WriteLine(
                    $"✅ Linking {provider} to existing user: {email}");
            }

            // ── LINK social login to user ──
            // Saved in AspNetUserLogins table:
            // LoginProvider = "Facebook"
            // ProviderKey   = Facebook's user ID
            var addLoginResult = await _userManager.AddLoginAsync(
                user,
                new UserLoginInfo(
                    provider,         // "Facebook"
                    providerId,       // Facebook user ID
                    provider          // display name
                ));

            if (!addLoginResult.Succeeded)
            {
                Console.WriteLine($"❌ Failed to link {provider}: " +
                    string.Join(", ", addLoginResult.Errors
                        .Select(e => e.Description)));
                // Still return user — they can login with password
            }

            return user;
        }
    }
}