// SmartNest.Shared/DTOs/NotificationDTOs.cs
namespace SmartNest.Client.Models
{
    public class NotificationDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info";
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public bool IsRead { get; set; }
        public DateTime Timestamp { get; set; }
        public double? TriggerValue { get; set; }
        public double? ThresholdValue { get; set; }
        public string? ActionTaken { get; set; }
    }

    public class NotificationSettingsDto
    {
        public string UserId { get; set; } = string.Empty;
        public bool NotificationsEnabled { get; set; } = true;
        public bool EmailNotifications { get; set; } = true;
        public bool PushNotifications { get; set; } = true;
        public bool SmsNotifications { get; set; } = false;
        public double TemperatureThreshold { get; set; } = 35.0;
        public int HumidityThreshold { get; set; } = 40;
        public int DustThreshold { get; set; } = 100;
        public int WaterLevelThreshold { get; set; } = 30;
        public int FoodLevelThreshold { get; set; } = 25;
        public int CheckInterval { get; set; } = 5;
        public DateTime LastUpdated { get; set; }
    }

    public class CreateNotificationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info";
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public double? TriggerValue { get; set; }
        public double? ThresholdValue { get; set; }
        public string? ActionTaken { get; set; }
    }
}