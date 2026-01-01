using System;
using System.Net;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace SmartNest.Server.Controllers.postgres
{
    [Route("odata/postgres/Sensordata")]
    [Authorize] // Nécessite une authentification
    public partial class SensordataController : ODataController
    {
        private SmartNest.Server.Data.ApplicationDbContext context;
        private readonly ILogger<SensordataController> _logger;

        public SensordataController(
            SmartNest.Server.Data.ApplicationDbContext context,
            ILogger<SensordataController> logger)
        {
            this.context = context;
            this._logger = logger;
        }

        /// <summary>
        /// Récupère l'UserId authentifié depuis les claims
        /// </summary>
        private string GetAuthenticatedUserId()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirst("sub")?.Value
                          ?? User.FindFirst("oid")?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    return userId;
                }
            }

            _logger.LogWarning("⚠️ Unable to retrieve authenticated user ID");
            return string.Empty;
        }

        /// <summary>
        /// Filtre les données pour ne retourner que celles de l'utilisateur authentifié
        /// </summary>
        [HttpGet]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult GetSensordata()
        {
            var userId = GetAuthenticatedUserId();
            
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            // Filtrer uniquement les données de l'utilisateur authentifié
            var items = this.context.SensorData
                .Where(s => s.userid == userId)
                .AsQueryable<SmartNest.Server.Models.postgres.Sensordatum>();
            
            this.OnSensordataRead(ref items);

            _logger.LogInformation($"📊 User {userId} retrieved sensor data");

            return Ok(items);
        }

        partial void OnSensordataRead(ref IQueryable<SmartNest.Server.Models.postgres.Sensordatum> items);

        partial void OnSensordatumGet(ref SingleResult<SmartNest.Server.Models.postgres.Sensordatum> item);

        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        [HttpGet("/odata/postgres/Sensordata(id={id})")]
        public IActionResult GetSensordatum(int key)
        {
            var userId = GetAuthenticatedUserId();
            
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            // Vérifier que la donnée appartient à l'utilisateur authentifié
            var items = this.context.SensorData
                .Where(i => i.id == key && i.userid == userId);
            
            var result = SingleResult.Create(items);

            OnSensordatumGet(ref result);

            if (!items.Any())
            {
                return NotFound(new { error = "Sensor data not found or access denied" });
            }

            return Ok(result);
        }

        partial void OnSensordatumDeleted(SmartNest.Server.Models.postgres.Sensordatum item);
        partial void OnAfterSensordatumDeleted(SmartNest.Server.Models.postgres.Sensordatum item);

        [HttpDelete("/odata/postgres/Sensordata(id={id})")]
        public IActionResult DeleteSensordatum(int key)
        {
            try
            {
                var userId = GetAuthenticatedUserId();
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Vérifier que la donnée appartient à l'utilisateur authentifié
                var item = this.context.SensorData
                    .Where(i => i.id == key && i.userid == userId)
                    .FirstOrDefault();

                if (item == null)
                {
                    return NotFound(new { error = "Sensor data not found or access denied" });
                }

                this.OnSensordatumDeleted(item);
                this.context.SensorData.Remove(item);
                this.context.SaveChanges();
                this.OnAfterSensordatumDeleted(item);

                _logger.LogInformation($"🗑️ User {userId} deleted sensor data {key}");

                return new NoContentResult();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"❌ Error deleting sensor data {key}");
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnSensordatumUpdated(SmartNest.Server.Models.postgres.Sensordatum item);
        partial void OnAfterSensordatumUpdated(SmartNest.Server.Models.postgres.Sensordatum item);

        [HttpPut("/odata/postgres/Sensordata(id={id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PutSensordatum(int key, [FromBody]SmartNest.Server.Models.postgres.Sensordatum item)
        {
            try
            {
                var userId = GetAuthenticatedUserId();
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                if(!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (item == null || (item.id != key))
                {
                    return BadRequest(new { error = "Invalid data" });
                }

                // Vérifier que la donnée appartient à l'utilisateur authentifié
                var existingItem = this.context.SensorData
                    .Where(i => i.id == key && i.userid == userId)
                    .FirstOrDefault();

                if (existingItem == null)
                {
                    return NotFound(new { error = "Sensor data not found or access denied" });
                }

                // S'assurer que l'userId ne peut pas être modifié
                item.userid = userId;

                this.OnSensordatumUpdated(item);
                this.context.SensorData.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.SensorData.Where(i => i.id == key);
                Request.QueryString = Request.QueryString.Add("$expand", "device");
                this.OnAfterSensordatumUpdated(item);

                _logger.LogInformation($"✏️ User {userId} updated sensor data {key}");

                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"❌ Error updating sensor data {key}");
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        [HttpPatch("/odata/postgres/Sensordata(id={id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PatchSensordatum(int key, [FromBody]Delta<SmartNest.Server.Models.postgres.Sensordatum> patch)
        {
            try
            {
                var userId = GetAuthenticatedUserId();
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                if(!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Vérifier que la donnée appartient à l'utilisateur authentifié
                var item = this.context.SensorData
                    .Where(i => i.id == key && i.userid == userId)
                    .FirstOrDefault();

                if (item == null)
                {
                    return NotFound(new { error = "Sensor data not found or access denied" });
                }

                patch.Patch(item);

                // S'assurer que l'userId ne peut pas être modifié
                item.userid = userId;

                this.OnSensordatumUpdated(item);
                this.context.SensorData.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.SensorData.Where(i => i.id == key);
                Request.QueryString = Request.QueryString.Add("$expand", "device");

                _logger.LogInformation($"✏️ User {userId} patched sensor data {key}");

                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"❌ Error patching sensor data {key}");
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnSensordatumCreated(SmartNest.Server.Models.postgres.Sensordatum item);
        partial void OnAfterSensordatumCreated(SmartNest.Server.Models.postgres.Sensordatum item);

        [HttpPost]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult Post([FromBody] SmartNest.Server.Models.postgres.Sensordatum item)
        {
            try
            {
                var userId = GetAuthenticatedUserId();
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                if(!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (item == null)
                {
                    return BadRequest(new { error = "Invalid data" });
                }

                // Forcer l'userId à celui de l'utilisateur authentifié
                item.userid = userId;
                item.timestamp = DateTime.UtcNow;

                this.OnSensordatumCreated(item);
                this.context.SensorData.Add(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.SensorData.Where(i => i.id == item.id);

                Request.QueryString = Request.QueryString.Add("$expand", "device");

                this.OnAfterSensordatumCreated(item);

                _logger.LogInformation($"➕ User {userId} created sensor data {item.id}");

                return new ObjectResult(SingleResult.Create(itemToReturn))
                {
                    StatusCode = 201
                };
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating sensor data");
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }
    }
}