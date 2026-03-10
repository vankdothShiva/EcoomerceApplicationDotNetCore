// Controllers/ProfileController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShopAPI.Models.Identity;
using ShopAPI.Models.Identity.DTOs;
using System.Security.Claims;
using WebApplication1.DTOs;

namespace ShopAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // ALL endpoints need valid JWT
    public class ProfileController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;

        public ProfileController(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        // ── GET my profile ──
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);

            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.Address,
                user.City,
                user.ProfilePic,
                user.CreatedAt,
                user.TwoFactorEnabled,
                user.AuthenticatorSetupComplete,
                user.LoginProvider,
                Roles = roles,
                Claims = claims.Select(c => new { c.Type, c.Value })
            });
        }

        // ── UPDATE my profile ──
        [HttpPut]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            // Update fields
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Address = dto.Address;
            user.City = dto.City;
            user.PhoneNumber = dto.Phone;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new
                {
                    errors = result.Errors.Select(e => e.Description)
                });

            // Update FullName claim too
            var oldClaim = (await _userManager.GetClaimsAsync(user))
                               .FirstOrDefault(c => c.Type == "FullName");
            if (oldClaim != null)
                await _userManager.ReplaceClaimAsync(user, oldClaim,
                    new System.Security.Claims.Claim(
                        "FullName", $"{dto.FirstName} {dto.LastName}"));

            return Ok(new { message = "Profile updated successfully!" });
        }

        // ── CHANGE PASSWORD ──
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            var result = await _userManager.ChangePasswordAsync(
                user, dto.CurrentPassword, dto.NewPassword);

            if (!result.Succeeded)
                return BadRequest(new
                {
                    errors = result.Errors.Select(e => e.Description)
                });

            return Ok(new { message = "Password changed successfully!" });
        }

        // ── DELETE my account ──
        [HttpDelete]
        public async Task<IActionResult> DeleteAccount(
            [FromBody] string password)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            // Verify password before deleting
            var passwordOk = await _userManager.CheckPasswordAsync(user, password);
            if (!passwordOk)
                return BadRequest(new { error = "Incorrect password." });

            // Soft delete — just deactivate
            user.IsActive = false;
            await _userManager.UpdateAsync(user);

            return Ok(new { message = "Account deactivated successfully." });
        }
    }
}