using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartNest.Server.Hubs;
using SmartNest.Server.Services;
using SmartNest.Server.Models;
using SmartNest.Server.Data;

namespace SmartNest.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VideoStreamController : ControllerBase
    {
        private readonly IVideoStreamService _videoService;
        private readonly IYoloAnalysisService _yoloService;
        private readonly IHubContext<RealtimeHub> _hubContext;
        private readonly ILogger<VideoStreamController> _logger;
        private readonly ApplicationDbContext _context;

        public VideoStreamController(
            IVideoStreamService videoService,
            IYoloAnalysisService yoloService,
            IHubContext<RealtimeHub> hubContext,
            ILogger<VideoStreamController> logger,
            ApplicationDbContext context)
        {
            _videoService = videoService;
            _yoloService = yoloService;
            _hubContext = hubContext;
            _logger = logger;
            _context = context;
        }

        // ✅ Démarrer une session de streaming
        [HttpPost("start-session")]
        public async Task<IActionResult> StartStreamSession([FromBody] StartStreamRequest request)
        {
            try
            {
                // Arrêter toute session active existante pour cet utilisateur
                var existingSession = await _context.VideoStreamSessions
                    .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.IsActive);

                if (existingSession != null)
                {
                    existingSession.IsActive = false;
                    existingSession.StoppedAt = DateTime.UtcNow;
                }

                // Créer nouvelle session
                var newSession = new VideoStreamSession
                {
                    UserId = request.UserId,
                    IsActive = true,
                    StartedAt = DateTime.UtcNow,
                    Quality = request.Resolution,
                    TargetFPS = request.DesiredFPS,
                    LastFrameReceived = DateTime.UtcNow
                };

                _context.VideoStreamSessions.Add(newSession);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ Stream session started for user {request.UserId}");

                return Ok(new
                {
                    sessionId = newSession.Id,
                    message = "Stream session started",
                    isActive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting stream session");
                return BadRequest(new { error = ex.Message });
            }
        }

        // ✅ Arrêter la session de streaming
        [HttpPost("stop-session")]
        public async Task<IActionResult> StopStreamSession([FromBody] StopStreamRequest request)
        {
            try
            {
                var session = await _context.VideoStreamSessions
                    .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.IsActive);

                if (session == null)
                {
                    return NotFound(new { error = "No active session found" });
                }

                session.IsActive = false;
                session.StoppedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ Stream session stopped for user {request.UserId}");

                return Ok(new
                {
                    message = "Stream session stopped",
                    isActive = false,
                    duration = (session.StoppedAt.Value - session.StartedAt).TotalMinutes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping stream session");
                return BadRequest(new { error = ex.Message });
            }
        }

        // ✅ Vérifier l'état de la session
        [HttpGet("session-status/{userId}")]
        public async Task<IActionResult> GetSessionStatus(string userId)
        {
            try
            {
                var session = await _context.VideoStreamSessions
                    .Where(s => s.UserId == userId && s.IsActive)
                    .OrderByDescending(s => s.StartedAt)
                    .FirstOrDefaultAsync();

                if (session == null)
                {
                    return Ok(new
                    {
                        isActive = false,
                        message = "No active session"
                    });
                }

                return Ok(new
                {
                    isActive = session.IsActive,
                    sessionId = session.Id,
                    startedAt = session.StartedAt,
                    quality = session.Quality,
                    targetFPS = session.TargetFPS,
                    lastFrameReceived = session.LastFrameReceived
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session status");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("frame")]
        public async Task<IActionResult> UploadFrame([FromBody] VideoFrameRequest request)
        {
            try
            {
                // Vérifier si une session active existe
                var activeSession = await _context.VideoStreamSessions
                    .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.IsActive);

                if (activeSession == null)
                {
                    return BadRequest(new { error = "No active stream session. Please start streaming first." });
                }

                // Mettre à jour le timestamp de la dernière frame
                activeSession.LastFrameReceived = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Sauvegarder la frame en DB
                var videoFrame = new VideoFrame
                {
                    UserId = request.UserId,
                    FrameData = request.FrameData,
                    Quality = request.Quality,
                    Timestamp = DateTime.UtcNow,
                    Size = System.Text.Encoding.UTF8.GetByteCount(request.FrameData)
                };

                _context.VideoFrames.Add(videoFrame);
                await _context.SaveChangesAsync();

                // Traiter la frame normalement
                await _videoService.ProcessVideoFrame(request.UserId, request.FrameData);

                return Ok(new { message = "Frame processed", frameId = videoFrame.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading video frame");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("analyze-recent/{userId}")]
        public async Task<IActionResult> AnalyzeRecentFrames(string userId, [FromQuery] int count = 1)
        {
            try
            {
                var recentFrames = await _context.VideoFrames
                    .Where(f => f.UserId == userId)
                    .OrderByDescending(f => f.Timestamp)
                    .Take(count)
                    .ToListAsync();

                if (!recentFrames.Any())
                {
                    return NotFound(new { error = "No recent frames found" });
                }

                var allDetections = new List<object>();

                foreach (var frame in recentFrames)
                {
                    var detections = await _yoloService.AnalyzeFrameAsync(frame.FrameData, userId);
                    allDetections.AddRange(detections);
                }

                return Ok(new
                {
                    framesAnalyzed = recentFrames.Count,
                    totalDetections = allDetections.Count,
                    detections = allDetections
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing recent frames");
                return BadRequest(new { error = ex.Message });
            }
        }

        // ✅ Récupérer les frames sauvegardées
        [HttpGet("saved-frames/{userId}")]
        public async Task<IActionResult> GetSavedFrames(
            string userId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            try
            {
                _logger.LogInformation($"📸 Getting saved frames for user {userId}");

                var query = _context.VideoFrames.Where(f => f.UserId == userId);

                if (fromDate.HasValue)
                {
                    // Convertir en UTC pour la comparaison
                    var fromUtc = fromDate.Value.ToUniversalTime();
                    query = query.Where(f => f.Timestamp >= fromUtc);
                    _logger.LogInformation($"  Filter: fromDate >= {fromUtc}");
                }

                if (toDate.HasValue)
                {
                    var toUtc = toDate.Value.ToUniversalTime();
                    query = query.Where(f => f.Timestamp <= toUtc);
                    _logger.LogInformation($"  Filter: toDate <= {toUtc}");
                }

                var frames = await query
                    .OrderByDescending(f => f.Timestamp)
                    .Take(100) // Limiter à 100 frames max pour les performances
                    .Select(f => new
                    {
                        Id = f.Id.ToString(), // Convertir en string pour compatibilité client
                        f.UserId,
                        f.FrameData,
                        f.Quality,
                        f.Timestamp,
                        f.Size
                    })
                    .ToListAsync();

                _logger.LogInformation($"✅ Found {frames.Count} frames");

                return Ok(frames);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved frames");
                return BadRequest(new { error = ex.Message });
            }
        }

       [HttpGet("saved-frames/{userId}/{frameId}")]
        public async Task<IActionResult> GetSavedFrame(string userId, int frameId)
        {
            try
            {
                var frame = await _context.VideoFrames
                    .Where(f => f.Id == frameId && f.UserId == userId)
                    .Select(f => new
                    {
                        Id = f.Id.ToString(),
                        f.UserId,
                        f.FrameData,
                        f.Quality,
                        f.Timestamp,
                        f.Size
                    })
                    .FirstOrDefaultAsync();

                if (frame == null)
                    return NotFound(new { error = "Frame not found" });

                return Ok(frame);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved frame");
                return BadRequest(new { error = ex.Message });
            }
        }

        // ✅ Supprimer une frame
        [HttpDelete("saved-frames/{userId}/{frameId}")]
        public async Task<IActionResult> DeleteSavedFrame(string userId, int frameId)
        {
            try
            {
                var frame = await _context.VideoFrames
                    .FirstOrDefaultAsync(f => f.Id == frameId && f.UserId == userId);

                if (frame == null)
                    return NotFound(new { error = "Frame not found" });

                _context.VideoFrames.Remove(frame);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Frame deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting saved frame");
                return BadRequest(new { error = ex.Message });
            }
        }
        
        // ✅ Supprimer plusieurs frames (par période)
        [HttpDelete("saved-frames/{userId}")]
        public async Task<IActionResult> DeleteSavedFrames(
            string userId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            try
            {
                var query = _context.VideoFrames.Where(f => f.UserId == userId);

                if (fromDate.HasValue)
                {
                    var fromUtc = fromDate.Value.ToUniversalTime();
                    query = query.Where(f => f.Timestamp >= fromUtc);
                }

                if (toDate.HasValue)
                {
                    var toUtc = toDate.Value.ToUniversalTime();
                    query = query.Where(f => f.Timestamp <= toUtc);
                }

                var framesToDelete = await query.ToListAsync();
                var count = framesToDelete.Count;

                _context.VideoFrames.RemoveRange(framesToDelete);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"🗑️ Deleted {count} frames for user {userId}");

                return Ok(new { deletedCount = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting saved frames");
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class StopStreamRequest
    {
        public string UserId { get; set; } = string.Empty;
    }
}