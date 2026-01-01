using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartNest.Server.Models;
using SmartNest.Server.Models.postgres;
using SmartNest.Server.Services;

namespace SmartNest.Server.Controllers.postgres
{
    /// <summary>
    /// API REST pour la gestion des poussins et l'analyse YOLO
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChicksController : ControllerBase
    {
        private readonly IChickMonitoringService _chickService;
        private readonly IYoloAnalysisService _yoloService;
        private readonly ILogger<ChicksController> _logger;

        public ChicksController(
            IChickMonitoringService chickService,
            IYoloAnalysisService yoloService,
            ILogger<ChicksController> logger)
        {
            _chickService = chickService;
            _yoloService = yoloService;
            _logger = logger;
        }

        /// <summary>
        /// Récupère les statistiques pour un utilisateur
        /// </summary>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(ChickStatistics), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatistics([FromQuery] string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    userId = GetCurrentUserId();
                }

                _logger.LogInformation("📊 Getting statistics for user: {UserId}", userId);
                
                var stats = await _chickService.GetStatisticsAsync(userId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting statistics");
                return StatusCode(500, new { error = "Failed to retrieve statistics" });
            }
        }

        /// <summary>
        /// Met à jour le statut de santé d'un poussin
        /// </summary>
        [HttpPut("{chickId}/health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateHealthStatus(
            string chickId,
            [FromBody] UpdateHealthStatusRequest request)
        {
            try
            {
                _logger.LogInformation("🔄 Updating health for {ChickId} to {Status}", 
                    chickId, request.HealthStatus);

                var success = await _chickService.UpdateHealthStatusAsync(chickId, request.HealthStatus);
                
                if (!success)
                {
                    return NotFound(new { error = $"Chick {chickId} not found" });
                }

                return Ok(new { message = "Health status updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating health status");
                return StatusCode(500, new { error = "Failed to update health status" });
            }
        }

        /// <summary>
        /// Met à jour l'âge et le poids d'un poussin
        /// </summary>
        [HttpPut("{chickId}/metrics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMetrics(
            string chickId,
            [FromBody] UpdateMetricsRequest request)
        {
            try
            {
                _logger.LogInformation("🔄 Updating metrics for {ChickId}: Age={Age}, Weight={Weight}", 
                    chickId, request.Age, request.Weight);

                var success = await _chickService.UpdateAgeAndWeightAsync(
                    chickId, 
                    request.Age, 
                    request.Weight);
                
                if (!success)
                {
                    return NotFound(new { error = $"Chick {chickId} not found" });
                }

                return Ok(new { message = "Metrics updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating metrics");
                return StatusCode(500, new { error = "Failed to update metrics" });
            }
        }

        /// <summary>
        /// Déclenche une analyse YOLO sur une image
        /// </summary>
        [HttpPost("analyze")]
        [ProducesResponseType(typeof(YoloAnalysisResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> AnalyzeFrame([FromBody] YoloAnalysisRequest request)
        {
            try
            {
                var userId = string.IsNullOrWhiteSpace(request.UserId) 
                    ? GetCurrentUserId() 
                    : request.UserId;

                _logger.LogInformation("🔍 Starting YOLO analysis for user: {UserId}", userId);

                var detections = await _yoloService.AnalyzeFrameAsync(request.FrameData, userId);

                return Ok(new YoloAnalysisResponse
                {
                    Success = true,
                    DetectionsCount = detections.Count,
                    Detections = detections,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during YOLO analysis");
                return StatusCode(500, new YoloAnalysisResponse
                {
                    Success = false,
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst("sub")?.Value
                ?? User.FindFirst("oid")?.Value
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                ?? "anonymous";
        }
    }

    // ============================================================
    //  DTOs (Data Transfer Objects)
    // ============================================================

    public class UpdateHealthStatusRequest
    {
        public string HealthStatus { get; set; } = "Healthy";
    }

    public class UpdateMetricsRequest
    {
        public int Age { get; set; }
        public double Weight { get; set; }
    }

    public class YoloAnalysisRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string FrameData { get; set; } = string.Empty;
    }

    public class YoloAnalysisResponse
    {
        public bool Success { get; set; }
        public int DetectionsCount { get; set; }
        public List<SmartNest.Server.Models.postgres.Chick> Detections { get; set; } = new();
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; }
    }
}