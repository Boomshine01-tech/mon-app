using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Deltas;
using System.ComponentModel.DataAnnotations.Schema;

using SmartNest.Server.Data;
using SmartNest.Server.Models;

namespace SmartNest.Server.Controllers
{
    [Authorize]
    [Route("odata/Identity/ApplicationUsers")]
    public partial class ApplicationUsersController : ODataController
    {
        private readonly ApplicationDbContext context;
        private readonly UserManager<ApplicationUser> userManager;


        private readonly IWebHostEnvironment env;

        public ApplicationUsersController(IWebHostEnvironment env, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            this.context = context;
            this.userManager = userManager;

            this.env = env;
        }

        partial void OnUsersRead(ref IQueryable<ApplicationUser> users);

        [EnableQuery]
        [HttpGet]
        public IEnumerable<ApplicationUser> Get()
        {
            var users = userManager.Users;

            if (env.EnvironmentName != "Development")
            {
                if(HttpContext.Request.Host.Value != null)
                {
                    var tenant = context.Tenants.ToList().Where(t => t.Hosts.Split(',').Where(h => h.Contains(HttpContext.Request.Host.Value)).Any()).FirstOrDefault();
                    if (tenant != null)
                    {
                        users = users.Where(r => r.TenantId == tenant.Id);
                    }  
                }
               
            }
            OnUsersRead(ref users);

            return users;
        }

        [EnableQuery]
        [HttpGet("{Id}")]
        public SingleResult<ApplicationUser> GetApplicationUser(string key)
        {
            var user = context.Users.Where(i => i.Id == key);

            return SingleResult.Create(user);
        }

        partial void OnUserDeleted(ApplicationUser user);

        [HttpDelete("{Id}")]
        public async Task<IActionResult> Delete(string key)
        {
            var user = await userManager.FindByIdAsync(key);

            if (user == null)
            {
                return NotFound();
            }

            OnUserDeleted(user);

            var result = await userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                return IdentityError(result);
            }

            return new NoContentResult();
        }

        partial void OnUserUpdated(ApplicationUser user);

        [HttpPatch("{Id}")]
        public async Task<IActionResult> Patch(string key, [FromBody]ApplicationUser data)
        {
            var user = await userManager.FindByIdAsync(key);

            if (user == null)
            {
                return NotFound();
            }

            OnUserUpdated(data);

            IdentityResult? result = null;


            userManager.UserValidators.Clear();
            user.Roles = null;

            result = await userManager.UpdateAsync(user);

            if (data.Roles != null)
            {
                result = await userManager.RemoveFromRolesAsync(user, await userManager.GetRolesAsync(user));

                if (result.Succeeded) 
                {
                   result = await userManager.AddToRolesAsync(user, data.Roles.Where(r => !string.IsNullOrWhiteSpace(r.Name)).Select(r => r.Name!));
                }
            }

            if (!string.IsNullOrEmpty(data.Password))
            {
                result = await userManager.RemovePasswordAsync(user);

                if (result.Succeeded)
                {
                    result = await userManager.AddPasswordAsync(user, data.Password);
                }

                if (!result.Succeeded)
                {
                    return IdentityError(result);
                }
            }

            if (result != null && !result.Succeeded)
            {
                return IdentityError(result);
            }

            return new NoContentResult();
        }

        partial void OnUserCreated(ApplicationUser user);

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ApplicationUser user)
        {
            user.UserName = user.Email;
            user.EmailConfirmed = true;
            var password = user.Password;
            var roles = user.Roles;
            user.Roles = null;

            userManager.UserValidators.Clear();

            if (context.Users.Any(u => u.TenantId == user.TenantId && u.UserName == user.Name))
            {
                ModelState.AddModelError("", "User with the same name already exist for this tenant.");
                return BadRequest(ModelState);
            }
            IdentityResult result = await userManager.CreateAsync(user, password);

            if (result.Succeeded && roles != null)
            {
                result = await userManager.AddToRolesAsync(user, roles.Where(r => !string.IsNullOrWhiteSpace(r.Name)).Select(r => r.Name!));
            }

            user.Roles = roles;

            if (result.Succeeded)
            {
                OnUserCreated(user);

                return Created($"odata/Identity/Users('{user.Id}')", user);
            }
            else
            {
                return IdentityError(result);
            }
        }

        private IActionResult IdentityError(IdentityResult result)
        {
            var message = string.Join(", ", result.Errors.Select(error => error.Description));

            return BadRequest(new { error = new { message } });
        }
    }

    [Authorize]
    [Route("odata/Identity/ApplicationTenants")]
    public partial class ApplicationTenantsController : ODataController
    {
        private ApplicationDbContext context;
        private IWebHostEnvironment env;

        public ApplicationTenantsController(IWebHostEnvironment env, ApplicationDbContext context)
        {
          this.context = context;
          this.env = env;
        }

        partial void OnTenantsRead(ref IQueryable<ApplicationTenant> tenants);

        [EnableQuery]
        [HttpGet]
        public IEnumerable<ApplicationTenant> Get()
        {
            if (HttpContext.User.Identity!.Name != "tenantsadmin")
            {
                return Enumerable.Empty<ApplicationTenant>();
            }
            
            var items = this.context.Tenants.AsQueryable<ApplicationTenant>();

            OnTenantsRead(ref items);

            return items;
        }

        [EnableQuery]
        [HttpGet("{Id}")]
        public async Task<IActionResult> GetApplicationTenant(string key)
        {
            var item = this.context.Tenants.Where(i=>i.Id == key).FirstOrDefault();

            return new ObjectResult(item);
        }

        partial void OnTenantDeleted(ApplicationTenant tenant);

        [HttpDelete("{Id}")]
        public async Task<IActionResult> Delete(string key)
        {
            try
            {
                if (HttpContext.User.Identity!.Name != "tenantsadmin")
                {
                    return new UnauthorizedResult();
                }

                if(!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var item = this.context.Tenants
                    .Where(i => i.Id == key)
                    .FirstOrDefault();

                if (item == null)
                {
                    ModelState.AddModelError("", "Item no longer available");
                    return BadRequest(ModelState);
                }

                this.OnTenantDeleted(item);
                this.context.Tenants.Remove(item);
                this.context.SaveChanges();

                return new NoContentResult();
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnTenantUpdated(ApplicationTenant tenant);

        [HttpPatch("{Id}")]
        public async Task<IActionResult> Patch(string key, [FromBody]Delta<ApplicationTenant> patch)
        {
            if (HttpContext.User.Identity!.Name != "tenantsadmin")
            {
                return new UnauthorizedResult();
            }

            if(!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var item = this.context.Tenants.Where(i => i.Id == key).FirstOrDefault();

            if (item == null)
            {
                ModelState.AddModelError("", "Item no longer available");
                return BadRequest(ModelState);
            }

            if (env.EnvironmentName != "Development")
            {
                if (context.Tenants.ToList().Where(t => t.Hosts.Split(',').Where(h => item.Hosts.Split(',').Contains(h)).Any()).Any())
                {
                    ModelState.AddModelError("", "Tenant with the same host already exist.");
                    return BadRequest(ModelState);
                }
            }

            patch.Patch(item);

            this.OnTenantUpdated(item);
            this.context.Tenants.Update(item);
            this.context.SaveChanges();

            var itemToReturn = this.context.Tenants.Where(i => i.Id == key);
            return new ObjectResult(SingleResult.Create(itemToReturn));
        }

        partial void OnTenantCreated(ApplicationTenant tenant);

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ApplicationTenant item)
        {
            if (HttpContext.User.Identity!.Name != "tenantsadmin")
            {
                return new UnauthorizedResult();
            }

            if(!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (item == null)
            {
                return BadRequest();
            }

            if (env.EnvironmentName != "Development")
            {
                if (context.Tenants.ToList().Where(t => t.Hosts.Split(',').Where(h => item.Hosts.Split(',').Contains(h)).Any()).Any())
                {
                    ModelState.AddModelError("", "Tenant with the same host already exist.");
                    return BadRequest(ModelState);
                }
            }
            
            this.OnTenantCreated(item);
            this.context.Tenants.Add(item);
            this.context.SaveChanges();

            var key = item.Id;

            var itemToReturn = this.context.Tenants.Where(i => i.Id == key).FirstOrDefault();

            return Created($"auth/ApplicationTenants({itemToReturn!.Id})", itemToReturn);
        }
    }
}