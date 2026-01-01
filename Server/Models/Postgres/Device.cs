using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SmartNest.Server.Models.postgres
{
    [Table("devices")]
    public class device
    {
        [Key]
        [Required]
        [MaxLength(100)]
        public string DeviceId { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string DeviceName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string DeviceType { get; set; } = string.Empty; // Fan, HeatLamp, Feeder, WaterDispenser
        
        [Required]
        public bool IsActive { get; set; } = false;
        
        [Required]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        [MaxLength(500)]
        public string StatusMessage { get; set; } = string.Empty;
        
        // Navigation property
        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }

       
    }
    
}