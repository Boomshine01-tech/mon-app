using Microsoft.EntityFrameworkCore;
using SmartNest.Server.Models;
using SmartNest.Server.Models.postgres;

namespace SmartNest.Server.Services
{
    public interface IChickMonitoringService
    {

        Task<ChickStatistics> GetStatisticsAsync(string userId);
        Task<bool> UpdateHealthStatusAsync(string chickId, string healthStatus);
        Task<bool> UpdateAgeAndWeightAsync(string chickId, int age, double weight);
        Task<int> ProcessYoloDetectionsAsync(string userId, List<Chick> detections);
    }

    public class ChickMonitoringService : IChickMonitoringService
    {
        private readonly Data.ApplicationDbContext _context;
        private readonly ILogger<ChickMonitoringService> _logger;

        public ChickMonitoringService(
            Data.ApplicationDbContext context,
            ILogger<ChickMonitoringService> logger)
        {
            _context = context;
            _logger = logger;
        }


        public async Task<ChickStatistics> GetStatisticsAsync(string userId)
        {
            try
            {
                var chicks = await _context.Chicks
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                if (!chicks.Any())
                {
                    return new ChickStatistics
                    {
                        TotalCount = 0,
                        LastUpdate = DateTime.UtcNow
                    };
                }

                return new ChickStatistics
                {
                    TotalCount = chicks.Count,
                    HealthyCount = chicks.Count(c => c.healthstate == "Healthy"),
                    WarningCount = chicks.Count(c => c.healthstate == "Warning"),
                    SickCount = chicks.Count(c => c.healthstate == "Sick"),
                    AverageWeight = chicks.Average(c => c.Weight),
                    AverageAge = chicks.Average(c => c.Age),
                    LastUpdate = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating statistics");
                return new ChickStatistics { LastUpdate = DateTime.UtcNow };
            }
        }

        

        public async Task<bool> UpdateHealthStatusAsync(string chickId, string healthStatus)
        {
            try
            {
                var chick = await _context.Chicks.FindAsync(chickId);
                if (chick == null) return false;

                chick.healthstate = healthStatus;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ Chick {chickId} health status updated to {healthStatus}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating chick {chickId}");
                return false;
            }
        }

        public async Task<bool> UpdateAgeAndWeightAsync(string chickId, int age, double weight)
        {
            try
            {
                var chick = await _context.Chicks.FindAsync(chickId);
                if (chick == null) return false;

                chick.Age = age;
                chick.Weight = weight;
                chick.LastUpdated = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ Chick {chickId} updated: {age}d, {weight}g");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating chick {chickId}");
                return false;
            }
        }

        public async Task<int> ProcessYoloDetectionsAsync(string userId, List<Chick> detections)
        {
            try
            {
                int processedCount = 0;

                foreach (var detection in detections)
                {
                    var chick = await _context.Chicks
                        .FirstOrDefaultAsync(c => c.ChickId == detection.ChickId);

                    if (chick == null)
                    {
                // Nouveau poussin détecté
                        chick = new Chick
                        {
                            ChickId = detection.ChickId,
                            UserId = userId,
                            X = detection.X,
                            Y = detection.Y,
                            Confidence = detection.Confidence,
                            Age = detection.Age > 0 ? detection.Age : 1,
                            Weight = detection.Weight > 0 ? detection.Weight : 40,
                            healthstate = DetermineHealthStatus(detection),
                            LastUpdated = DateTime.UtcNow
                        };

                        await _context.Chicks.AddAsync(chick);
                    }
                    else
                    {
                        // Mise à jour d’un poussin existant
                        chick.X = detection.X;
                        chick.Y = detection.Y;
                        chick.Confidence = detection.Confidence;

                        if (detection.Weight > 0)
                            chick.Weight = detection.Weight;

                        if (detection.Age > chick.Age)
                            chick.Age = detection.Age;

                        chick.healthstate = DetermineHealthStatus(detection);
                        chick.LastUpdated = DateTime.UtcNow;
                    }

                    processedCount++;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Processed {processedCount} YOLO detections.");

                return processedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing YOLO detections");
                return 0;
            }
        }


        private string DetermineHealthStatus(Chick chick)
        {
            if (chick.Confidence < 0.5)
                return "Warning";

            if (chick.Weight < 30)
                return "Sick";

            if (chick.Weight > 150)
                return "Healthy";

            return "Healthy";
        }

    }
}