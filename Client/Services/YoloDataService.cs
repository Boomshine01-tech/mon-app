using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using SmartNest.Server.Models;
using SmartNest.Server.Models.postgres;

namespace SmartNest.Client.Services
{
     public interface IYoloDataService
    {
        Task<List<Chick>> GetYoloDetectionsAsync();
        Task<ChickStatistics> GetStatisticsAsync();
        Task<bool> UpdateHealthStatusAsync(string chickId, string healthStatus);
        Task<bool> UpdateAgeAndWeightAsync(string chickId, int age, double weight);
        Task<List<Chick>> AnalyzeFrameAsync(string base64Image);

    }
    public class YoloDataService : IYoloDataService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthenticationStateProvider _authProvider;
        private readonly ILogger<YoloDataService> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public YoloDataService(
            HttpClient httpClient,
            AuthenticationStateProvider authProvider,
            ILogger<YoloDataService> logger)
        {
            _httpClient = httpClient;
            _authProvider = authProvider;
            _logger = logger;
        }

        public async Task<List<Chick>> GetYoloDetectionsAsync()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync();
                _logger.LogInformation("📡 Fetching chicks for user: {UserId}", userId);

                var response = await _httpClient.GetAsync($"odata/postgres/Chicks?$filter=UserId eq '{userId}'");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ Failed to fetch chicks: {Status}", response.StatusCode);
                    return new List<Chick>();
                }

                var odataResponse = await response.Content.ReadFromJsonAsync<ODataResponse<Chick>>(_jsonOptions);
                
                var chicks = odataResponse?.Value ?? new List<Chick>();
                _logger.LogInformation("✅ Fetched {Count} chicks", chicks.Count);

                return chicks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching chicks");
                return new List<Chick>();
            }
        }

        public async Task<ChickStatistics> GetStatisticsAsync()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync();
                _logger.LogInformation("📊 Fetching statistics for user: {UserId}", userId);

                var response = await _httpClient.GetAsync($"api/chicks/statistics?userId={userId}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ Failed to fetch statistics: {Status}", response.StatusCode);
                    return new ChickStatistics { LastUpdate = DateTime.UtcNow };
                }

                var stats = await response.Content.ReadFromJsonAsync<ChickStatistics>(_jsonOptions);
                _logger.LogInformation("✅ Statistics fetched successfully");

                return stats ?? new ChickStatistics { LastUpdate = DateTime.UtcNow };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching statistics");
                return new ChickStatistics { LastUpdate = DateTime.UtcNow };
            }
        }

        public async Task<bool> UpdateHealthStatusAsync(string chickId, string healthStatus)
        {
            try
            {
                _logger.LogInformation("🔄 Updating health status for {ChickId} to {Status}", chickId, healthStatus);

                var response = await _httpClient.PutAsJsonAsync(
                    $"api/chicks/{chickId}/health",
                    new { HealthStatus = healthStatus },
                    _jsonOptions
                );

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Health status updated");
                    return true;
                }

                _logger.LogWarning("⚠️ Failed to update health status: {Status}", response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating health status");
                return false;
            }
        }

        public async Task<bool> UpdateAgeAndWeightAsync(string chickId, int age, double weight)
        {
            try
            {
                _logger.LogInformation("🔄 Updating {ChickId}: Age={Age}, Weight={Weight}", chickId, age, weight);

                var response = await _httpClient.PutAsJsonAsync(
                    $"api/chicks/{chickId}/metrics",
                    new { Age = age, Weight = weight },
                    _jsonOptions
                );

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Age and weight updated");
                    return true;
                }

                _logger.LogWarning("⚠️ Failed to update metrics: {Status}", response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating metrics");
                return false;
            }
        }

        public async Task<List<Chick>> AnalyzeFrameAsync(string base64Image)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync();
                _logger.LogInformation("🔍 Starting YOLO analysis for user: {UserId}", userId);

                var payload = new
                {
                    UserId = userId,
                    FrameData = base64Image
                };

                var response = await _httpClient.PostAsJsonAsync("api/yolo/analyze", payload, _jsonOptions);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ YOLO analysis failed: {Status}", response.StatusCode);
                    return new List<Chick>();
                }

                var chicks = await response.Content.ReadFromJsonAsync<List<Chick>>(_jsonOptions);
                _logger.LogInformation("✅ Analysis complete: {Count} chicks detected", chicks?.Count ?? 0);

                return chicks ?? new List<Chick>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during YOLO analysis");
                return new List<Chick>();
            }
        }

        private async Task<string> GetCurrentUserIdAsync()
        {
            var authState = await _authProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user?.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirst("sub")?.Value
                          ?? user.FindFirst("oid")?.Value
                          ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                          ?? "anonymous";

                return userId;
            }

            return "anonymous";
        }

        // Helper class pour la désérialisation OData
        private class ODataResponse<T>
        {
            public List<T> Value { get; set; } = new();
        }
    }
}