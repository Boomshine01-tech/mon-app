using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartNest.Server.Services;
using System.Security.Claims;

namespace SmartNest.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MqttConnectionController : ControllerBase
    {
        private readonly MqttService _mqttService;
        private readonly ILogger<MqttConnectionController> _logger;

        public MqttConnectionController(MqttService mqttService, ILogger<MqttConnectionController> logger)
        {
            _mqttService = mqttService;
            _logger = logger;
        }

        [HttpPost("connect")]
        public async Task<IActionResult> ConnectToMqtt([FromBody] MqttConnectionRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("❌ User ID not found in token claims");
            
                    // Déboguer les claims disponibles
                    _logger.LogInformation("📋 Available claims in token:");
                    foreach (var claim in User.Claims)
                    {
                        _logger.LogInformation($"   - {claim.Type}: {claim.Value}");
                    }
            
                    return Unauthorized(new { success = false, message = "User ID not found in token" });
                }

                _logger.LogInformation($"🔄 Connecting user {userId} to MQTT broker");

                await _mqttService.ConnectUserBroker(
                    userId, 
                    request.BrokerUrl ?? "192.168.1.24", 
                    request.Port ?? 1883
                );

                return Ok(new
                {
                    success = true,
                    message = $"User {userId} connected to MQTT broker",
                    userId = userId,
                    isConnected = _mqttService.IsConnected
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to connect user to MQTT broker");
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost("disconnect")]
        public async Task<IActionResult> DisconnectFromMqtt()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                _logger.LogInformation($"Disconnecting user {userId} from MQTT broker");
                
                await _mqttService.Disconnect();

                return Ok(new
                {
                    success = true,
                    message = "Disconnected from MQTT broker"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disconnect from MQTT broker");
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("status")]
        public IActionResult GetConnectionStatus()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            return Ok(new
            {
                isConnected = _mqttService.IsConnected,
                currentUserId = _mqttService.CurrentUserId,
                userId = userId
            });
        }

        [HttpPost("send-config/{macAddress}")]
        public async Task<IActionResult> SendConfigToESP32(string macAddress)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found");
                }

                await _mqttService.SendUserIdToSpecificESP32(macAddress, userId);

                return Ok(new
                {
                    success = true,
                    message = $"Configuration sent to ESP32 {macAddress}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }

    public class MqttConnectionRequest
    {
        public string? BrokerUrl { get; set; }
        public int? Port { get; set; }
    }
}