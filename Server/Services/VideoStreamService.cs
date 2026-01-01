using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.SignalR;
using SmartNest.Server.Hubs;
using SmartNest.Server.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SmartNest.Server.Services
{
    public interface IVideoStreamService
    {
        Task ProcessVideoFrame(string userId, string frameData);
        Task<string> GetRecentVideo(string userId, int minutesBack);
        Task<List<SavedFrameInfo>> GetSavedFrames(string userId, DateTime? fromDate, DateTime? toDate);
        Task<SavedFrameInfo?> GetSavedFrame(string frameId, string userId);
        Task<bool> DeleteSavedFrame(string frameId, string userId);
        Task<int> DeleteSavedFrames(string userId, DateTime? fromDate, DateTime? toDate);
        void CleanupOldFrames();
    }

    public class VideoStreamService : IVideoStreamService
    {
        private readonly IHubContext<RealtimeHub> _hubContext;
        private readonly ILogger<VideoStreamService> _logger;
        private readonly ConcurrentDictionary<string, LinkedList<VideoFrame>> _userFrames;
        private readonly int _maxFramesPerUser = 100;
        private readonly TimeSpan _frameRetention = TimeSpan.FromHours(24);
        private readonly HttpClient _httpClient;

        public VideoStreamService(
            IHubContext<RealtimeHub> hubContext, 
            ILogger<VideoStreamService> logger,
            HttpClient httpClient)
        {
            _hubContext = hubContext;
            _logger = logger;
            _httpClient = httpClient;
            _userFrames = new ConcurrentDictionary<string, LinkedList<VideoFrame>>();
        }

        public async Task ProcessVideoFrame(string userId, string frameData)
        {
            try
            {
                // Optimisation: Vérifier la taille de la frame
                if (frameData.Length > 50000)
                {
                    _logger.LogWarning($"Frame too large for user {userId}: {frameData.Length} bytes");
                    return;
                }

                var frame = new VideoFrame
                {
                    FrameData = frameData,
                    Timestamp = DateTime.UtcNow,
                    Id = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0)

                };

                // Stockage en mémoire temporaire
                StoreFrame(userId, frame);

                // Diffusion en temps réel via SignalR
                await _hubContext.Clients.Group(userId)
                    .SendAsync("VideoFrameReceived", new
                    {
                        FrameId = frame.Id,
                        Data = frameData,
                        Timestamp = frame.Timestamp
                    });

                _logger.LogDebug($"Video frame processed for user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing video frame for user {userId}");
            }
        }

        private void StoreFrame(string userId, VideoFrame frame)
        {
            var frames = _userFrames.GetOrAdd(userId, new LinkedList<VideoFrame>());
            
            lock (frames)
            {
                frames.AddLast(frame);
                
                while (frames.Count > _maxFramesPerUser)
                {
                    frames.RemoveFirst();
                }
            }
        }

        public async Task<string> GetRecentVideo(string userId, int minutesBack)
        {
            if (_userFrames.TryGetValue(userId, out var frames))
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-minutesBack);
                var recentFrames = frames.Where(f => f.Timestamp >= cutoff).ToList();
                
                return JsonSerializer.Serialize(new
                {
                    UserId = userId,
                    Frames = recentFrames,
                    Count = recentFrames.Count
                });
            }
            
            return "{}";
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

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<SavedFrameInfo>>() ?? new List<SavedFrameInfo>();
                }
                
                _logger.LogWarning($"Failed to get saved frames: {response.StatusCode}");
                return new List<SavedFrameInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved frames for user {UserId}", userId);
                return new List<SavedFrameInfo>();
            }
        }

        public async Task<SavedFrameInfo?> GetSavedFrame(string frameId, string userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/videostream/saved-frames/{userId}/{frameId}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<SavedFrameInfo>();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved frame {FrameId} for user {UserId}", frameId, userId);
                return null;
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
                _logger.LogError(ex, "Error deleting saved frame {FrameId} for user {UserId}", frameId, userId);
                return false;
            }
        }

        public async Task<int> DeleteSavedFrames(string userId, DateTime? fromDate, DateTime? toDate)
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
                _logger.LogError(ex, "Error deleting saved frames for user {UserId}", userId);
                return 0;
            }
        }

        public void CleanupOldFrames()
        {
            var cutoff = DateTime.UtcNow.Subtract(_frameRetention);
            
            foreach (var userFrames in _userFrames)
            {
                lock (userFrames.Value)
                {
                    var current = userFrames.Value.First;
                    while (current != null)
                    {
                        var next = current.Next;
                        if (current.Value.Timestamp < cutoff)
                        {
                            userFrames.Value.Remove(current);
                        }
                        current = next;
                    }
                }
            }
        }
    }



    public class SavedFrameInfo
    {
        public string Id { get; set; } = default!;
        public string UserId { get; set; } = default!;
        public string FrameData { get; set; } = default!;
        public string Quality { get; set; } = default!;
        public DateTime Timestamp { get; set; }
        public int Size { get; set; }
    }

    public class DeleteResult
    {
        public int DeletedCount { get; set; }
    }
}