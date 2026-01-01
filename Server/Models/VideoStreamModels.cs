using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNest.Server.Models
{
    public class SavedFrameInfo
    {
    public string Id { get; set; } = string.Empty; // ✅ String pour compatibilité
    public string UserId { get; set; } = string.Empty;
    public string FrameData { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int Size { get; set; }
    }

    public class HistoryPeriod
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

   

    public class VideoFrameRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string FrameData { get; set; } = string.Empty;
        public string Quality { get; set; } = "medium";
    }

    public class StartStreamRequest
    {
        public string UserId { get; set; } = string.Empty;
        public int DesiredFPS { get; set; } = 10;
        public string Resolution { get; set; } = "640x480";
    }
    public class VideoStreamSession
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        public bool IsActive { get; set; }
        
        public DateTime StartedAt { get; set; }
        
        public DateTime? StoppedAt { get; set; }
        
        public string Quality { get; set; } = "medium";
        
        public int TargetFPS { get; set; } = 10;
        
        public double CompressionQuality { get; set; } = 0.7;
        
        public DateTime LastFrameReceived { get; set; }
    }
}