using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNest.Server.Models.postgres
{
    public class Chick
    {
        [Key]
        public string ChickId { get; set; } = string.Empty;
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public double Confidence { get; set; }
        [Required]
        public int X { get; set; } 
        [Required]
        public int Y { get; set; } 
        public string healthstate { get; set; } = string.Empty;
        public int Age { get; set; } // en jours
        public double Weight { get; set; } // en grammes
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public bool IsHealthy => healthstate == "Healthy";

        [NotMapped]
        public bool IsHighConfidence => Confidence >= 0.7;

        [NotMapped]
        public string AgeCategory => Age switch
        {
            <= 7 => "Nouveau-né",
            <= 21 => "Jeune",
            <= 42 => "Adolescent",
            _ => "Adulte"
        };

        [NotMapped]
        public string WeightCategory => Weight switch
        {
            < 50 => "Très léger",
            < 100 => "Léger",
            < 150 => "Normal",
            < 200 => "Lourd",
            _ => "Très lourd"
        };
        
        public override string ToString()
        {
            return $"Chick {ChickId}: {healthstate}, Age={Age}d, Weight={Weight:F1}g, Confidence={Confidence:P0}";
        }

    }

   
    public class ChickStatistics
    {
        [Key]
        public int TotalCount { get; set; }
        [Required]
        public int HealthyCount { get; set; }
        [Required]
        public int WarningCount { get; set; }
        [Required]
        public int SickCount { get; set; }
        public double AverageWeight { get; set; }
        public double AverageAge { get; set; }
        [Required]
        public DateTime LastUpdate { get; set; }
        [NotMapped]
        public double HealthyPercentage => TotalCount > 0 
            ? (double)HealthyCount / TotalCount * 100 
            : 0;
        [NotMapped]
        public double SickPercentage => TotalCount > 0 
            ? (double)SickCount / TotalCount * 100 
            : 0;
        [NotMapped]
        public bool IsFlockHealthy => HealthyPercentage >= 80;
        [NotMapped]
        public string AlertLevel => SickPercentage switch
        {
            >= 20 => "Critical",
            >= 10 => "High",
            >= 5 => "Medium",
            _ => "Low"
        };
        
        public override string ToString()
        {
            return $"Total: {TotalCount}, Healthy: {HealthyCount} ({HealthyPercentage:F1}%), " +
                   $"Warning: {WarningCount}, Sick: {SickCount} ({SickPercentage:F1}%)";
        }
    }

    /// <summary>
    /// Réponse de l'API YOLO
    /// </summary>
    public class YoloDetectionResponse
    {
        public List<Chick> Detections { get; set; } = new();
        public int TotalDetected { get; set; }
        public DateTime Timestamp { get; set; }
        public string CameraId { get; set; } = string.Empty;
    }
}