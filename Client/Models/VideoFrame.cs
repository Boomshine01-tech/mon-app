// Models/VideoFrame.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SmartNest.Client.Models
{
    public class VideoFrame
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public string FrameData { get; set; } = string.Empty; // Base64
        
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        public string Quality { get; set; } = "medium";
        public int Size { get; set; } // Taille en bytes
        public double CompressionRatio { get; set; } = 0.7;
    }

    public class SavedFrameInfo
    {
        public int Id { get; set; }
        public string FrameData { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Quality { get; set; } = "medium";
        public int Size { get; set; }
    }
}