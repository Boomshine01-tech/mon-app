// SmartNest.Client/Services/NotificationUIService.cs
using System.Net.Http.Json;
using SmartNest.Client.Models;

namespace SmartNest.Client.Services
{
    public interface INotificationUIService
    {
        Task<List<NotificationDto>> GetUserNotificationsAsync(string userId);
        Task<NotificationDto?> GetNotificationByIdAsync(string notificationId, string userId);
        Task<NotificationDto?> CreateNotificationAsync(CreateNotificationRequest request);
        Task<bool> MarkAsReadAsync(string notificationId, string userId);
        Task<bool> MarkAllAsReadAsync(string userId);
        Task<bool> DeleteNotificationAsync(string notificationId, string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task<List<NotificationDto>> GetNotificationsByCategoryAsync(string userId, string category);
        Task<List<NotificationDto>> GetNotificationsBySeverityAsync(string userId, string severity);
        Task<bool> DeleteOldNotificationsAsync(string userId, int daysOld = 30);
        
        // Settings
        Task<NotificationSettingsDto?> GetNotificationSettingsAsync(string userId);
        Task<NotificationSettingsDto?> SaveNotificationSettingsAsync(NotificationSettingsDto settings);
        Task<bool> ToggleNotificationsAsync(string userId, bool enabled);
    }

    public class NotificationUIService : INotificationUIService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NotificationUIService> _logger;

        public NotificationUIService(HttpClient httpClient, ILogger<NotificationUIService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<NotificationDto>> GetUserNotificationsAsync(string userId)
        {
            try
            {
                
                var response = await _httpClient.GetFromJsonAsync<List<NotificationDto>>($"api/notifications/{userId}");
                return response ?? new List<NotificationDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des notifications");
                return new List<NotificationDto>();
            }
        }

        public async Task<NotificationDto?> GetNotificationByIdAsync(string notificationId, string userId)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<NotificationDto>($"api/notifications/{notificationId}/details/{userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de la notification {NotificationId}", notificationId);
                return null;
            }
        }

        public async Task<NotificationDto?> CreateNotificationAsync(CreateNotificationRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/notifications", request);
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<NotificationDto>();
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création de la notification");
                return null;
            }
        }

        public async Task<bool> MarkAsReadAsync(string notificationId, string userId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/notifications/{notificationId}/mark-read?userId={userId}", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du marquage comme lu");
                return false;
            }
        }

        public async Task<bool> MarkAllAsReadAsync(string userId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/notifications/{userId}/mark-all-read", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du marquage de toutes les notifications");
                return false;
            }
        }

        public async Task<bool> DeleteNotificationAsync(string notificationId, string userId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/notifications/{notificationId}?userId={userId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression de la notification");
                return false;
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<Dictionary<string, int>>($"api/notifications/{userId}/unread-count");
                return response?.GetValueOrDefault("count", 0) ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du comptage des notifications non lues");
                return 0;
            }
        }

        public async Task<List<NotificationDto>> GetNotificationsByCategoryAsync(string userId, string category)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<NotificationDto>>($"api/notifications/{userId}/category/{category}");
                return response ?? new List<NotificationDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération par catégorie");
                return new List<NotificationDto>();
            }
        }

        public async Task<List<NotificationDto>> GetNotificationsBySeverityAsync(string userId, string severity)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<NotificationDto>>($"api/notifications/{userId}/severity/{severity}");
                return response ?? new List<NotificationDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération par sévérité");
                return new List<NotificationDto>();
            }
        }

        public async Task<bool> DeleteOldNotificationsAsync(string userId, int daysOld = 30)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/notifications/{userId}/old?daysOld={daysOld}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression des anciennes notifications");
                return false;
            }
        }

        // ===== SETTINGS METHODS =====

        public async Task<NotificationSettingsDto?> GetNotificationSettingsAsync(string userId)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<NotificationSettingsDto>($"api/notifications/settings/{userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des paramètres");
                return null;
            }
        }

        public async Task<NotificationSettingsDto?> SaveNotificationSettingsAsync(NotificationSettingsDto settings)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/notifications/settings", settings);
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<NotificationSettingsDto>();
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la sauvegarde des paramètres");
                return null;
            }
        }

        public async Task<bool> ToggleNotificationsAsync(string userId, bool enabled)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/notifications/settings/{userId}/toggle", enabled);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du toggle des notifications");
                return false;
            }
        }
    }
}