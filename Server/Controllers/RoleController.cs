using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartNest.Server.Models;

namespace SmartNest.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly RoleManager<ApplicationRole> _roleManager;

        public RolesController(RoleManager<ApplicationRole> roleManager)
        {
            _roleManager = roleManager;
        }

        [HttpGet]
        public IActionResult GetRoles()
        {
            var roles = _roleManager.Roles.ToList();
            return Ok(roles);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return NotFound();
            
            return Ok(role);
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] ApplicationRole role)
        {
            if (role == null || string.IsNullOrEmpty(role.Name))
                return BadRequest("Role name is required");

            var result = await _roleManager.CreateAsync(role);
            
            if (result.Succeeded)
                return Ok(role);
            
            return BadRequest(result.Errors);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return NotFound();

            var result = await _roleManager.DeleteAsync(role);
            
            if (result.Succeeded)
                return Ok();
            
            return BadRequest(result.Errors);
        }
    }
}