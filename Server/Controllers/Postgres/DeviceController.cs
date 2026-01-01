using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNest.Server.Services;
using SmartNest.Server.Models.postgres;

namespace SmartNest.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DevicesController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;
        private readonly MqttService _mqttService;
        private readonly ILogger<DevicesController> _logger;

        public DevicesController(
            Data.ApplicationDbContext context,
            MqttService mqttService,
            ILogger<DevicesController> logger)
        {
            _context = context;
            _mqttService = mqttService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<device>>> GetDevices()
        {
            try
            {
               
                var devices = await _context.Devices
                    .Select(d => new device
                    {
                        DeviceId = d.DeviceId,
                        DeviceName = d.DeviceName,
                        DeviceType = d.DeviceType,
                        IsActive = d.IsActive,
                        LastUpdated = d.LastUpdated,
                        StatusMessage = d.StatusMessage
                    })
                    .ToListAsync();

                return Ok(devices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching devices");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/devices/{deviceId}
        [HttpGet("{deviceId}")]
        public async Task<ActionResult<device>> GetDevice(string deviceId)
        {
            try
            {
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.DeviceId == deviceId);

                if (device == null)
                    return NotFound();

                var status = new device
                {
                    DeviceId = device.DeviceId,
                    DeviceName = device.DeviceName,
                    DeviceType = device.DeviceType,
                    IsActive = device.IsActive,
                    LastUpdated = device.LastUpdated,
                    StatusMessage = device.StatusMessage
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching device {deviceId}");
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/devices/{deviceId}/toggle
        [HttpPost("{deviceId}/toggle")]
        public async Task<ActionResult<bool>> ToggleDevice(string deviceId, [FromBody] bool activate)
        {
            try
            {
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.DeviceId == deviceId);

                if (device == null)
                    return NotFound();

                

                // 2️⃣ Publier la commande MQTT vers l'ESP32
                var success = await _mqttService.PublishDeviceCommand(
                    device.UserId, 
                    deviceId, 
                    activate
                );

                if (!success)
                {
                    _logger.LogWarning($"⚠️ MQTT command failed for device {deviceId}");
                }

                _logger.LogInformation($"✅ Device {deviceId} toggled to {activate}");
                
                return Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling device {deviceId}");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}