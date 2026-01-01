using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNest.Server.Data;
using SmartNest.Server.Models;

namespace SmartNest.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TenantsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TenantsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetTenants()
        {
            var tenants = await _context.Tenants
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Hosts
                })
                .ToListAsync();

            return Ok(tenants);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTenant(string id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null)
                return NotFound();

            return Ok(new
            {
                tenant.Id,
                tenant.Name,
                tenant.Hosts
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateTenant([FromBody] CreateTenantModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.Name))
                return BadRequest("Name is required");

            var tenant = new ApplicationTenant
            {
                Id = Guid.NewGuid().ToString(),
                Name = model.Name,
                Hosts = model.Hosts
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                tenant.Id,
                tenant.Name,
                tenant.Hosts
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTenant(string id, [FromBody] UpdateTenantModel model)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null)
                return NotFound();

            tenant.Name = model.Name;
            tenant.Hosts = model.Hosts;

            _context.Tenants.Update(tenant);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                tenant.Id,
                tenant.Name,
                tenant.Hosts
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTenant(string id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null)
                return NotFound();

            _context.Tenants.Remove(tenant);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }

    public class CreateTenantModel
    {
        public string Name { get; set; } = null!;
        public string Hosts { get; set; } = null!;
    }

    public class UpdateTenantModel
    {
        public string Name { get; set; } = null!;
        public string Hosts { get; set; } = null!;
    }
}