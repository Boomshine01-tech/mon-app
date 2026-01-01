using SmartNest.Server.Models.postgres;

namespace SmartNest.Client.Services
{
    public interface INotificationService
    {
        Task<List<Notification>> GetNotificationsAsync();
        Task<NotificationSettings> GetNotificationSettingsAsync();
        Task SaveNotificationSettingsAsync(NotificationSettings settings);
        Task MarkAsReadAsync(string notificationId);
        Task MarkAllAsReadAsync();
        Task DeleteNotificationAsync(string notificationId);
    }
}