namespace SmartNest.Server.Models
{
    public class ChickData
    {
        public string Id { get; set; } = string.Empty;
        public string HealthStatus { get; set; } = "Unknown";
        public int Age { get; set; }
        public double TheoreticalWeight { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public double Confidence { get; set; }
        public string Zone { get; set; } = "";
    }
    
    public class ChickHealthAnalysis
    {
        public string OverallHealth { get; set; } = "Unknown";
        public int HealthyCount { get; set; }
        public int SickCount { get; set; }
        public DateTime AnalysisDate { get; set; } = DateTime.Now;
    }

    public class YoloDetection
    {
        public int Id { get; set; }
        public string? ChickId { get; set; }
        public string? HealthStatus { get; set; }
        public int AgeDays { get; set; }
        public float EstimatedWeight { get; set; }
        public DateTime Timestamp { get; set; }
        public float Confidence { get; set; }
        public string? Zone { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }
}