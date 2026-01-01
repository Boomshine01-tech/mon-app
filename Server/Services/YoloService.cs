using System.Net.Http.Json;
using System.Text.Json;
using SmartNest.Server.Models.postgres;

namespace SmartNest.Server.Services
{
    public interface IYoloAnalysisService
    {
        Task<List<Chick>> AnalyzeFrameAsync(string frameData, string userId);
        Task<YoloAnalysisResult> SendToYoloApiAsync(string base64Image);
        Task ProcessAndStoreDetectionsAsync(string userId, List<Chick> detections);
    }

    public class YoloAnalysisService : IYoloAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly IChickMonitoringService _chickService;
        private readonly ILogger<YoloAnalysisService> _logger;
        private readonly string _yoloApiUrl;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public YoloAnalysisService(
            HttpClient httpClient,
            IChickMonitoringService chickService,
            ILogger<YoloAnalysisService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _chickService = chickService;
            _logger = logger;

            _httpClient.Timeout = TimeSpan.FromSeconds(10); // ⚡ Timeout optimisé

            _yoloApiUrl = configuration["YoloApi:Url"] ?? "http://localhost:5000/detect";
        }

        // ============================================================
        //  MAIN ANALYSIS PIPELINE
        // ============================================================

        public async Task<List<Chick>> AnalyzeFrameAsync(string frameData, string userId)
        {
            var traceId = Guid.NewGuid().ToString("N"); // 🔎 Correlation ID

            _logger.LogInformation(
                "🔍 [{Trace}] Starting YOLO analysis | User: {User} | Image length: {Len}",
                traceId, userId, frameData?.Length ?? 0
            );

            try
            {
                var yoloResult = await SendToYoloApiAsync(frameData!);

                if (yoloResult?.Detections == null || yoloResult.Detections.Count == 0)
                {
                    _logger.LogWarning("⚠️ [{Trace}] No detections found", traceId);
                    return new List<Chick>(0);
                }

                // Conversion optimisée
                var chicks = ConvertDetections(yoloResult, userId);

                // Sauvegarde BDD
                await ProcessAndStoreDetectionsAsync(userId, chicks);

                _logger.LogInformation(
                    "✅ [{Trace}] Analysis complete | {Count} chicks detected",
                    traceId, chicks.Count
                );

                return chicks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [{Trace}] Error during YOLO analysis", traceId);
                return new List<Chick>(0);
            }
        }

        // ============================================================
        //  YOLO API CALL
        // ============================================================

        public async Task<YoloAnalysisResult> SendToYoloApiAsync(string base64Image)
        {
            try
            {
                // Nettoyage base64
                if (base64Image.Contains(','))
                    base64Image = base64Image[(base64Image.IndexOf(',') + 1)..];

                var request = new
                {
                    image = base64Image,
                    confidence = 0.5,
                    model = "yolov8n"
                };

                _logger.LogInformation("📤 Sending image to YOLO: {Url}", _yoloApiUrl);

                var response = await _httpClient.PostAsJsonAsync(_yoloApiUrl, request, _jsonOptions);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ YOLO status: {Status}", response.StatusCode);
                    return new YoloAnalysisResult();
                }

                var result = await response.Content.ReadFromJsonAsync<YoloAnalysisResult>();

                if (result == null)
                {
                    _logger.LogWarning("⚠️ YOLO returned empty JSON");
                    return new YoloAnalysisResult();
                }

                _logger.LogInformation("📦 YOLO returned {Count} detections", result.Detections.Count);

                return result;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "⏳ Timeout contacting YOLO API");
                throw new Exception("YOLO API timeout — server likely overloaded.", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "❌ YOLO API unreachable");
                throw new Exception("YOLO API unreachable — verify the container/service is running.", ex);
            }
        }

        private List<Chick> ConvertDetections(YoloAnalysisResult result, string userId)
        {
            int count = result.Detections.Count;
            var list = new List<Chick>(capacity: count); // ⚡ performance

            var timestamp = DateTime.UtcNow;

            foreach (var d in result.Detections)
            {
                double area = d.BoundingBox.Width * d.BoundingBox.Height;

                list.Add(new Chick
                {
                    ChickId = $"CHK_{(int)d.BoundingBox.X}_{(int)d.BoundingBox.Y}",
                    X = (int)d.BoundingBox.X,
                    Y = (int)d.BoundingBox.Y,
                    Confidence = d.Confidence,
                    healthstate = DetermineHealth(area, d.Confidence),
                    Age = EstimateAge(area),
                    Weight = EstimateWeight(area),
                    LastUpdated = timestamp,
                    UserId = userId
                });
            }

            return list;
        }

        // ============================================================
        //  HEALTH / AGE / WEIGHT OPTIMIZED RULES
        // ============================================================

        private string DetermineHealth(double area, double confidence)
        {
            if (confidence < 0.6)
                return "Warning";

            if (area < 1000)
                return "Sick";

            if (area > 10000)
                return "Healthy";

            return "Warning";
        }

        private int EstimateAge(double area)
        {
            if (area < 2000) return 1;
            if (area < 5000) return 7;
            if (area < 8000) return 14;
            return 21;
        }

        private double EstimateWeight(double area)
        {
            return 40 + (area / 100.0);
        }

        // ============================================================
        //  DATABASE SAVE
        // ============================================================

        public async Task ProcessAndStoreDetectionsAsync(string userId, List<Chick> detections)
        {
            try
            {
                _logger.LogInformation(
                    "💾 Saving {Count} detections for user {UserId}",
                    detections.Count, userId
                );

                int saved = await _chickService.ProcessYoloDetectionsAsync(userId, detections);

                _logger.LogInformation("✅ Stored {Count} detections", saved);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error saving detections");
                throw;
            }
        }
    }

    // ============================================================
    //  MODELS
    // ============================================================

    public class YoloAnalysisResult
    {
        public List<YoloDetection> Detections { get; set; } = new();
        public double ProcessingTime { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
    }

    public class YoloDetection
    {
        public string Class { get; set; } = "chick";
        public double Confidence { get; set; }
        public BoundingBox BoundingBox { get; set; } = new();
    }

    public class BoundingBox
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
