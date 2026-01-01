using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SmartNest.Server.Models;

namespace SmartNest.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
         private readonly ILogger<UsersController> _logger;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, ILogger<UsersController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            var userId = GetUserIdFromClaims();
            
            if (string.IsNullOrEmpty(userId) || userId == "anonymous")
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            return Ok(new 
            { 
                userId = userId,
                userName = User.Identity?.Name,
                isAuthenticated = User.Identity?.IsAuthenticated ?? false
            });
        }

        [HttpGet("id")]
        public IActionResult GetUserId()
        {
            var userId = GetUserIdFromClaims();
            
            if (string.IsNullOrEmpty(userId) || userId == "anonymous")
            {
                return Unauthorized();
            }

            return Ok(userId);
        }

        [HttpGet]
        public IActionResult GetUsers()
        {
            var users = _userManager.Users.Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email
            }).ToList();
            
            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var allRoles = _roleManager.Roles.ToList();
            var userRoleIds = allRoles.Where(r => roles.Contains(r.Name!)).Select(r => r.Id).ToList();

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                RoleIds = userRoleIds
            });
        }

        private string GetUserIdFromClaims()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirst("sub")?.Value
                          ?? User.FindFirst("oid")?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                          ?? "anonymous";

                _logger.LogInformation($"User ID retrieved: {userId}");
                return userId;
            }

            return "anonymous";
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
                return BadRequest("Email and password are required");

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // Ajouter les rôles
            if (model.RoleIds != null && model.RoleIds.Any())
            {
                var roleNames = _roleManager.Roles
                    .Where(r => model.RoleIds.Contains(r.Id))
                    .Select(r => r.Name)
                    .ToList();

                await _userManager.AddToRolesAsync(user, roleNames!);
            }

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserModel model)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            // Mettre à jour l'email
            user.Email = model.Email;
            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
                return BadRequest(updateResult.Errors);

            // Mettre à jour le mot de passe si fourni
            if (!string.IsNullOrEmpty(model.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.Password);

                if (!passwordResult.Succeeded)
                    return BadRequest(passwordResult.Errors);
            }

            // Mettre à jour les rôles
            var currentRoles = await _userManager.GetRolesAsync(user);
            var selectedRoleNames = _roleManager.Roles
                .Where(r => model.RoleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToList();

            var rolesToRemove = currentRoles.Except(selectedRoleNames).ToList();
            var rolesToAdd = selectedRoleNames.Except(currentRoles).ToList();

            if (rolesToRemove.Any())
                await _userManager.RemoveFromRolesAsync(user, rolesToRemove!);

            if (rolesToAdd.Any())
                await _userManager.AddToRolesAsync(user, rolesToAdd!);

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
                return Ok();

            return BadRequest(result.Errors);
        }

        [HttpGet("roles")]
        public IActionResult GetAllRoles()
        {
            var roles = _roleManager.Roles.Select(r => new
            {
                r.Id,
                r.Name
            }).ToList();

            return Ok(roles);
        }
    }

    public class CreateUserModel
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
        public List<string> RoleIds { get; set; } = new();
    }

    public class UpdateUserModel
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
        public List<string> RoleIds { get; set; } = new();
    }
}