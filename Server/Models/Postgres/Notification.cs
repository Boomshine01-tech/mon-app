
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNest.Server.Models.postgres
{

    public class CreateNotificationRequest
    {
        
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info"; // Critical, Warning, Info, Success
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public double? TriggerValue { get; set; }
        public double? ThresholdValue { get; set; }
        public string? ActionTaken { get; set; }
    }

    [Table("notifications")]
    public class Notification
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        [Required]
        public string UserId { get; set; } = string.Empty;
        [Required]
        public string Title { get; set; } = string.Empty;
        [Required]
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info";
        public bool IsRead { get; set; } = false;
        public DateTime Timestamp { get; set; }
        public DateTime UpdatedAt { get; set; }
        public double? TriggerValue { get; set; }
        public double? ThresholdValue { get; set; }
        public string ActionTaken { get; set; } = string.Empty;
    }

    [Table("notificationSettings")]
    public class NotificationSettings
    {
        [Key]
        public string UserId { get; set; } = string.Empty;
        [Required]
        public bool NotificationsEnabled { get; set; } = true;
        public bool EmailNotifications { get; set; } = true;
        public bool PushNotifications { get; set; } = true;
        public bool SmsNotifications { get; set; } = false;
        public double TemperatureThreshold { get; set; } = 35.0;
        public int HumidityThreshold { get; set; } = 40;
        public int DustThreshold { get; set; } = 100;
        public int WaterLevelThreshold { get; set; } = 30;
        public int FoodLevelThreshold { get; set; } = 25;
        public int CheckInterval { get; set; } = 5; // en minutes
        public DateTime LastUpdated { get; set; }
    }

    [Table("notificationStats")]
    public class NotificationStats
    {
        [Required]
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int CriticalCount { get; set; }
        public int TodayCount { get; set; }
        public Dictionary<string, int> ByCategoryCount { get; set; } = new();
        public Dictionary<string, int> BySeverityCount { get; set; } = new();
    }
}