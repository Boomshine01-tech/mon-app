using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNest.Server.Models;
using SmartNest.Server.Services;
using SmartNest.Server.Data;

namespace SmartNest.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;
        private readonly IConfiguration _configuration;

        public AccountController(
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IConfiguration configuration,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _configuration = configuration;
            _logger = logger;
            
            Console.WriteLine("✅ AccountController initialized");
        }

        // ========================================
        // GET /api/account/user/{id}
        // Endpoint appelé par SecurityService.GetUserById()
        // ========================================
        [HttpGet("user/{id}")]
        public async Task<IActionResult> GetUserById(string id)
        {
            try
            {
                Console.WriteLine($"🔍 GetUserById called - ID: {id}");
                
                if (string.IsNullOrEmpty(id))
                {
                    Console.WriteLine("❌ ID is empty");
                    return BadRequest(new { error = "User ID required" });
                }
                
                var user = await _userManager.FindByIdAsync(id);
                
                if (user == null)
                {
                    Console.WriteLine($"⚠️  User {id} not found");
                    return NotFound(new { error = $"User {id} not found" });
                }
                
                Console.WriteLine($"✅ User found: {user.UserName}");
                
                // Retourner un objet compatible avec ApplicationUser du client
                var userDto = new 
                {
                    id = user.Id,
                    userName = user.UserName,
                    email = user.Email,
                    emailConfirmed = user.EmailConfirmed,
                    phoneNumber = user.PhoneNumber,
                    // Ajoutez d'autres propriétés si nécessaire
                };
                
                return Ok(userDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GetUserById error: {ex.Message}");
                _logger.LogError(ex, "Error getting user by ID");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // ========================================
        // POST /api/account/login
        // ========================================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                Console.WriteLine("🔐 Login attempt");
                Console.WriteLine($"   Username: {request.UserName}");
                
                if (string.IsNullOrEmpty(request.UserName) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { error = "Username and password required" });
                }
                
                var user = await _userManager.FindByNameAsync(request.UserName);
                
                if (user == null)
                {
                    Console.WriteLine("❌ User not found");
                    return Unauthorized(new { error = "Invalid credentials" });
                }
                
                if (!user.EmailConfirmed)
                {
                    Console.WriteLine("❌ Email not confirmed");
                    return Unauthorized(new { error = "Email not confirmed" });
                }
                
                var result = await _signInManager.PasswordSignInAsync(
                    request.UserName, 
                    request.Password, 
                    request.RememberMe, 
                    lockoutOnFailure: false);
                
                if (result.Succeeded)
                {
                    Console.WriteLine("✅ Login successful");
                    
                    // Connexion MQTT optionnelle
                    try
                    {
                        var mqttService = HttpContext.RequestServices.GetService<MqttService>();
                        if (mqttService != null)
                        {
                            await mqttService.ConnectUserBroker(user.Id, "192.168.1.7", 1883);
                            Console.WriteLine("📡 MQTT connected");
                        }
                    }
                    catch (Exception mqttEx)
                    {
                        Console.WriteLine($"⚠️  MQTT connection failed: {mqttEx.Message}");
                    }
                    
                    return Ok(new 
                    {
                        success = true,
                        userId = user.Id,
                        userName = user.UserName,
                        email = user.Email
                    });
                }
                
                if (result.IsLockedOut)
                {
                    return Unauthorized(new { error = "Account locked" });
                }
                
                return Unauthorized(new { error = "Invalid credentials" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Login error: {ex.Message}");
                _logger.LogError(ex, "Login error");
                return StatusCode(500, new { error = "Login failed" });
            }
        }

        // ========================================
        // POST /api/account/register
        // ========================================
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                Console.WriteLine("📝 Registration attempt");
                Console.WriteLine($"   Email: {request.UserName}");
                
                if (string.IsNullOrEmpty(request.UserName) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { error = "Email and password required" });
                }
                
                var existingUser = await _userManager.FindByEmailAsync(request.UserName);
                if (existingUser != null)
                {
                    Console.WriteLine($"❌ User already exists: {request.UserName}");
                    return BadRequest(new { error = "User already exists" });
                }
                
                var user = new ApplicationUser
                {
                    UserName = request.UserName,
                    Email = request.UserName,
                    EmailConfirmed = true // Pour simplifier, à changer en production
                };
                
                var result = await _userManager.CreateAsync(user, request.Password);
                
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    Console.WriteLine($"❌ Registration failed: {errors}");
                    return BadRequest(new { error = errors });
                }
                
                Console.WriteLine($"✅ User created: {user.Id}");
                
                // Ajouter au rôle User
                if (!await _roleManager.RoleExistsAsync("User"))
                {
                    await _roleManager.CreateAsync(new ApplicationRole { Name = "User" });
                    Console.WriteLine("➕ Role 'User' created");
                }
                
                await _userManager.AddToRoleAsync(user, "User");
                Console.WriteLine("✅ User added to role 'User'");
                
                return Ok(new 
                { 
                    success = true,
                    message = "Registration successful",
                    userId = user.Id
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Registration error: {ex.Message}");
                _logger.LogError(ex, "Registration error");
                return StatusCode(500, new { error = "Registration failed" });
            }
        }

        // ========================================
        // POST /api/account/logout
        // ========================================
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                Console.WriteLine("🚪 Logout");
                await _signInManager.SignOutAsync();
                Console.WriteLine("✅ Logout successful");
                
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Logout error: {ex.Message}");
                _logger.LogError(ex, "Logout error");
                return StatusCode(500, new { error = "Logout failed" });
            }
        }

        // ========================================
        // POST /api/account/currentuser
        // Retourne l'état d'authentification actuel
        // ========================================
        [HttpPost("currentuser")]
        public IActionResult CurrentUser()
        {
            try
            {
                Console.WriteLine("👤 CurrentUser called");
                Console.WriteLine($"   Authenticated: {User.Identity?.IsAuthenticated}");
                Console.WriteLine($"   Name: {User.Identity?.Name}");
                
                var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
                var name = User.Identity?.Name ?? string.Empty;
                
                var claims = User.Claims.Select(c => new 
                { 
                    type = c.Type, 
                    value = c.Value 
                }).ToList();
                
                return Ok(new 
                {
                    isAuthenticated = isAuthenticated,
                    name = name,
                    claims = claims
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ CurrentUser error: {ex.Message}");
                _logger.LogError(ex, "CurrentUser error");
                return StatusCode(500, new { error = "Failed to get current user" });
            }
        }

        // ========================================
        // POST /api/account/changepassword
        // ========================================
        [HttpPost("changepassword")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                Console.WriteLine("🔐 Change password attempt");
                
                if (string.IsNullOrEmpty(request.OldPassword) || string.IsNullOrEmpty(request.NewPassword))
                {
                    return BadRequest(new { error = "Old and new password required" });
                }
                
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }
                
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { error = "User not found" });
                }
                
                var result = await _userManager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
                
                if (result.Succeeded)
                {
                    Console.WriteLine("✅ Password changed successfully");
                    return Ok(new { success = true, message = "Password changed" });
                }
                
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                Console.WriteLine($"❌ Password change failed: {errors}");
                return BadRequest(new { error = errors });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Change password error: {ex.Message}");
                _logger.LogError(ex, "Change password error");
                return StatusCode(500, new { error = "Failed to change password" });
            }
        }

        // ========================================
        // DTOs (Data Transfer Objects)
        // ========================================
        public class LoginRequest
        {
            public string UserName { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public bool RememberMe { get; set; }
        }

        public class RegisterRequest
        {
            public string UserName { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class ChangePasswordRequest
        {
            public string OldPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
    }
}
