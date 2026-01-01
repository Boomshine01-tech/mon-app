using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartNest.Server.Models;
using System.Security.Claims;

namespace SmartNest.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(userName);
            if (user == null)
                return NotFound();

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email
            });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(userName);
            if (user == null)
                return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);

            if (result.Succeeded)
                return Ok(new { message = "Password changed successfully" });

            return BadRequest(result.Errors);
        }
    }

    public class ChangePasswordModel
    {
        public string OldPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
    }
}