// Server/Models/VideoFrame.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNest.Server.Models
{
    public class VideoFrame
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] 

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

    // Modèle pour l'API (optionnel)
    public class VideoFrameDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FrameData { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Quality { get; set; } = "medium";
    }
}