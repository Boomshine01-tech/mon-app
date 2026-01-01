using System.Net.Http;
using System.Net.Http.Json;

namespace SmartNest.Client.Services
{
    public interface IVideoStreamService
    {
        Task ProcessVideoFrame(string userId, string frameData, bool analyzeWithYolo = false);
        Task<List<SavedFrameInfo>> GetSavedFrames(string userId, DateTime? fromDate, DateTime? toDate);
        Task<bool> DeleteSavedFrame(string frameId, string userId);
        Task<int> DeleteAllSavedFrames(string userId, DateTime? fromDate, DateTime? toDate);
        Task<AnalysisResult> AnalyzeRecentFrames(string userId, int count = 1);
        
        // ✅ NOUVEAU
        Task<bool> StartStreamSession(StartStreamRequest request);
        Task<bool> StopStreamSession(string userId);
        Task<SessionStatusResponse?> GetSessionStatus(string userId);
    }

    public class VideoStreamService : IVideoStreamService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<VideoStreamService> _logger;

        public VideoStreamService(HttpClient httpClient, ILogger<VideoStreamService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        // ✅ NOUVEAU: Démarrer une session de streaming
        public async Task<bool> StartStreamSession(StartStreamRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/videostream/start-session", request);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Stream session started");
                    return true;
                }
                
                _logger.LogWarning($"⚠️ Failed to start session: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error starting stream session");
                return false;
            }
        }

        // ✅ NOUVEAU: Arrêter une session de streaming
        public async Task<bool> StopStreamSession(string userId)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/videostream/stop-session", new { UserId = userId });
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Stream session stopped");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error stopping stream session");
                return false;
            }
        }

        // ✅ NOUVEAU: Récupérer le statut de la session
        public async Task<SessionStatusResponse?> GetSessionStatus(string userId)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<SessionStatusResponse>(
                    $"api/videostream/session-status/{userId}");
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting session status");
                return null;
            }
        }

        public async Task ProcessVideoFrame(string userId, string frameData, bool analyzeWithYolo = false)
        {
            try
            {
                var request = new VideoFrameRequest
                {
                    UserId = userId,
                    FrameData = frameData,
                    Quality = "medium",
                    AnalyzeWithYolo = analyzeWithYolo
                };

                var response = await _httpClient.PostAsJsonAsync("api/videostream/frame", request);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"⚠️ Failed to process video frame: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing video frame");
            }
        }

        public async Task<List<SavedFrameInfo>> GetSavedFrames(string userId, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var url = $"api/videostream/saved-frames/{userId}";
                var queryParams = new List<string>();
                
                if (fromDate.HasValue)
                    queryParams.Add($"fromDate={Uri.EscapeDataString(fromDate.Value.ToString("o"))}");
                if (toDate.HasValue)
                    queryParams.Add($"toDate={Uri.EscapeDataString(toDate.Value.ToString("o"))}");
                
                if (queryParams.Any())
                    url += "?" + string.Join("&", queryParams);

                var response = await _httpClient.GetFromJsonAsync<List<SavedFrameInfo>>(url);
                return response ?? new List<SavedFrameInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting saved frames");
                return new List<SavedFrameInfo>();
            }
        }

        public async Task<bool> DeleteSavedFrame(string frameId, string userId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/videostream/saved-frames/{userId}/{frameId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting saved frame");
                return false;
            }
        }

        public async Task<int> DeleteAllSavedFrames(string userId, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var url = $"api/videostream/saved-frames/{userId}";
                var queryParams = new List<string>();
                
                if (fromDate.HasValue)
                    queryParams.Add($"fromDate={Uri.EscapeDataString(fromDate.Value.ToString("o"))}");
                if (toDate.HasValue)
                    queryParams.Add($"toDate={Uri.EscapeDataString(toDate.Value.ToString("o"))}");
                
                if (queryParams.Any())
                    url += "?" + string.Join("&", queryParams);

                var response = await _httpClient.DeleteAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<DeleteResult>();
                    return result?.DeletedCount ?? 0;
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting saved frames");
                return 0;
            }
        }

        public async Task<AnalysisResult> AnalyzeRecentFrames(string userId, int count = 1)
        {
            try
            {
                _logger.LogInformation($"🔍 Analyzing {count} recent frame(s) with YOLO...");

                var response = await _httpClient.PostAsync(
                    $"api/videostream/analyze-recent/{userId}?count={count}",
                    null
                );

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<AnalysisResult>();
                    
                    if (result != null)
                    {
                        _logger.LogInformation($"✅ YOLO detected {result.UniqueChicks} unique chicks");
                        return result;
                    }
                }

                _logger.LogWarning("⚠️ YOLO analysis returned no results");
                return new AnalysisResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error analyzing frames with YOLO");
                throw;
            }
        }
    }

    // ✅ NOUVEAU: Modèle de réponse pour le statut de session
    public class SessionStatusResponse
    {
        public bool IsActive { get; set; }
        public int SessionId { get; set; }
        public DateTime StartedAt { get; set; }
        public string Quality { get; set; } = "medium";
        public int TargetFPS { get; set; } = 10;
        public DateTime LastFrameReceived { get; set; }
    }

    public class DeleteResult
    {
        public int DeletedCount { get; set; }
    }
     public class VideoFrameRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string FrameData { get; set; } = string.Empty;
        public string Quality { get; set; } = "medium";
        public bool AnalyzeWithYolo { get; set; } = false;
    }

    public class SavedFrameInfo
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string FrameData { get; set; } = string.Empty;
        public string Quality { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int Size { get; set; }
    }

    public class AnalysisResult
    {
        public int FramesAnalyzed { get; set; }
        public int TotalDetections { get; set; }
        public int UniqueChicks { get; set; }
        public List<ChickDetection> Detections { get; set; } = new();
    }

    public class ChickDetection
    {
        public string Id { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public double Confidence { get; set; }
        public string HealthStatus { get; set; } = string.Empty;
    }

    public class QualityOption
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
        public class StartStreamRequest
    {
        public string UserId { get; set; } = string.Empty;
        public int DesiredFPS { get; set; } = 10;
        public string Resolution { get; set; } = "640x480";
    }
}