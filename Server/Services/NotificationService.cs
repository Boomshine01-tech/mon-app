// SmartNest.Server/Services/NotificationService.cs
using Microsoft.EntityFrameworkCore;
using SmartNest.Server.Data;
using SmartNest.Server.Models.postgres;


namespace SmartNest.Server.Services
{
    public interface INotificationService
    {
        Task<List<Notification>> GetUserNotificationsAsync(string userId);
        Task<Notification?> GetNotificationByIdAsync(string notificationId, string userId);
        Task<Notification> CreateNotificationAsync(CreateNotificationRequest request);
        Task<bool> MarkAsReadAsync(string notificationId, string userId);
        Task<bool> MarkAllAsReadAsync(string userId);
        Task<bool> DeleteNotificationAsync(string notificationId, string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task<List<Notification>> GetNotificationsByCategoryAsync(string userId, string category);
        Task<List<Notification>> GetNotificationsBySeverityAsync(string userId, string severity);
        Task<bool> DeleteOldNotificationsAsync(string userId, int daysOld = 30);
        Task<NotificationStats> GetNotificationStatsAsync(string userId);
    }
    public interface INotificationSettingsService
    {
        Task<NotificationSettings?> GetSettingsAsync(string userId);
        Task<NotificationSettings> CreateOrUpdateSettingsAsync(NotificationSettings settings);
        Task<bool> ToggleNotificationsAsync(string userId, bool enabled);
        Task<bool> UpdateThresholdsAsync(string userId, NotificationSettings settings);
        Task<bool> DeleteSettingsAsync(string userId);
    }

    /// <summary>
    /// Service de gestion des notifications utilisateur
    /// </summary>
    public class NotificationUIService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationUIService> _logger;

        public NotificationUIService(
            ApplicationDbContext context,
            ILogger<NotificationUIService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.Timestamp)
                    .Take(100)
                    .ToListAsync();

                return notifications.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des notifications pour {UserId}", userId);
                throw;
            }
        }

        public async Task<Notification?> GetNotificationByIdAsync(string notificationId, string userId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                return notification != null ? MapToDto(notification) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de la notification {NotificationId}", notificationId);
                throw;
            }
        }

        public async Task<Notification> CreateNotificationAsync(CreateNotificationRequest request)
        {
            try
            {
                var notification = new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = request.UserId,
                    Title = request.Title,
                    Message = request.Message,
                    Category = request.Category,
                    Severity = request.Severity,
                    TriggerValue = request.TriggerValue,
                    ThresholdValue = request.ThresholdValue,
                    ActionTaken = request.ActionTaken!,
                    IsRead = false,
                    Timestamp = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Notification créée: {NotificationId} - {Title} pour {UserId}", 
                    notification.Id, 
                    notification.Title, 
                    request.UserId);

                return MapToDto(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création de la notification");
                throw;
            }
        }

        public async Task<bool> MarkAsReadAsync(string notificationId, string userId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                if (notification == null)
                    return false;

                notification.IsRead = true;
                notification.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du marquage comme lu");
                throw;
            }
        }

        public async Task<bool> MarkAllAsReadAsync(string userId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                    notification.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                
                _logger.LogInformation(
                    "{Count} notifications marquées comme lues pour {UserId}", 
                    notifications.Count, 
                    userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du marquage global");
                throw;
            }
        }

        public async Task<bool> DeleteNotificationAsync(string notificationId, string userId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                if (notification == null)
                    return false;

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression");
                throw;
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            try
            {
                return await _context.Notifications
                    .CountAsync(n => n.UserId == userId && !n.IsRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du comptage");
                throw;
            }
        }

        public async Task<List<Notification>> GetNotificationsByCategoryAsync(string userId, string category)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId && n.Category == category)
                    .OrderByDescending(n => n.Timestamp)
                    .ToListAsync();

                return notifications.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération par catégorie");
                throw;
            }
        }

        public async Task<List<Notification>> GetNotificationsBySeverityAsync(string userId, string severity)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId && n.Severity == severity)
                    .OrderByDescending(n => n.Timestamp)
                    .ToListAsync();

                return notifications.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération par sévérité");
                throw;
            }
        }

        public async Task<bool> DeleteOldNotificationsAsync(string userId, int daysOld = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

                var oldNotifications = await _context.Notifications
                    .Where(n => n.UserId == userId && n.Timestamp < cutoffDate)
                    .ToListAsync();

                _context.Notifications.RemoveRange(oldNotifications);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "{Count} anciennes notifications supprimées pour {UserId}", 
                    oldNotifications.Count, 
                    userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression des anciennes notifications");
                throw;
            }
        }

        public async Task<NotificationStats> GetNotificationStatsAsync(string userId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .ToListAsync();

                var today = DateTime.UtcNow.Date;

                return new NotificationStats
                {
                    TotalCount = notifications.Count,
                    UnreadCount = notifications.Count(n => !n.IsRead),
                    CriticalCount = notifications.Count(n => n.Severity == "Critical"),
                    TodayCount = notifications.Count(n => n.Timestamp.Date == today),
                    ByCategoryCount = notifications
                        .GroupBy(n => n.Category)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    BySeverityCount = notifications
                        .GroupBy(n => n.Severity)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des statistiques");
                throw;
            }
        }

        private Notification MapToDto(Notification notification)
        {
            return new Notification
            {
                Id = notification.Id,
                UserId = notification.UserId,
                Title = notification.Title,
                Message = notification.Message,
                Category = notification.Category,
                Severity = notification.Severity,
                IsRead = notification.IsRead,
                Timestamp = notification.Timestamp,
                UpdatedAt= notification.UpdatedAt,
                TriggerValue = notification.TriggerValue,
                ThresholdValue = notification.ThresholdValue,
                ActionTaken = notification.ActionTaken ?? string.Empty
            };
        }
    }
    public class NotificationSettingsService : INotificationSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationSettingsService> _logger;

        public NotificationSettingsService(
            ApplicationDbContext context,
            ILogger<NotificationSettingsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Récupère les paramètres de notification d'un utilisateur
        /// Crée des paramètres par défaut si aucun n'existe
        /// </summary>
        /// <param name="userId">ID de l'utilisateur</param>
        /// <returns>Paramètres de notification ou null en cas d'erreur</returns>
        public async Task<NotificationSettings?> GetSettingsAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("GetSettingsAsync appelé avec userId vide");
                    return null;
                }

                var settings = await _context.NotificationSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (settings == null)
                {
                    _logger.LogInformation("Aucun paramètre trouvé pour {UserId}, création des paramètres par défaut", userId);
                    
                    // Créer des paramètres par défaut
                    settings = new NotificationSettings
                    {
                        UserId = userId,
                        NotificationsEnabled = true,
                        EmailNotifications = true,
                        PushNotifications = true,
                        SmsNotifications = false,
                        TemperatureThreshold = 35.0,
                        HumidityThreshold = 40,
                        DustThreshold = 100,
                        WaterLevelThreshold = 30,
                        FoodLevelThreshold = 25,
                        CheckInterval = 5,
                        LastUpdated = DateTime.UtcNow,

                    };

                    _context.NotificationSettings.Add(settings);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Paramètres par défaut créés pour {UserId}", userId);
                }

                return MapToDto(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des paramètres pour {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Crée ou met à jour les paramètres de notification
        /// </summary>
        /// <param name="settingsDto">DTO des paramètres à sauvegarder</param>
        /// <returns>Paramètres sauvegardés</returns>
        public async Task<NotificationSettings> CreateOrUpdateSettingsAsync(NotificationSettings settingsDto)
        {
            try
            {
                if (string.IsNullOrEmpty(settingsDto.UserId))
                {
                    throw new ArgumentException("UserId ne peut pas être vide", nameof(settingsDto));
                }

                // Vérifier si des paramètres existent déjà
                var existingSettings = await _context.NotificationSettings
                    .FirstOrDefaultAsync(s => s.UserId == settingsDto.UserId);

                if (existingSettings == null)
                {
                    // Créer de nouveaux paramètres
                    var newSettings = new NotificationSettings
                    {
                        UserId = settingsDto.UserId,
                        NotificationsEnabled = settingsDto.NotificationsEnabled,
                        EmailNotifications = settingsDto.EmailNotifications,
                        PushNotifications = settingsDto.PushNotifications,
                        SmsNotifications = settingsDto.SmsNotifications,
                        TemperatureThreshold = settingsDto.TemperatureThreshold,
                        HumidityThreshold = settingsDto.HumidityThreshold,
                        DustThreshold = settingsDto.DustThreshold,
                        WaterLevelThreshold = settingsDto.WaterLevelThreshold,
                        FoodLevelThreshold = settingsDto.FoodLevelThreshold,
                        CheckInterval = settingsDto.CheckInterval,
                        LastUpdated = DateTime.UtcNow,
                    };

                    _context.NotificationSettings.Add(newSettings);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Nouveaux paramètres créés pour {UserId}", settingsDto.UserId);
                    return MapToDto(newSettings);
                }
                else
                {
                    // Mettre à jour les paramètres existants
                    existingSettings.NotificationsEnabled = settingsDto.NotificationsEnabled;
                    existingSettings.EmailNotifications = settingsDto.EmailNotifications;
                    existingSettings.PushNotifications = settingsDto.PushNotifications;
                    existingSettings.SmsNotifications = settingsDto.SmsNotifications;
                    existingSettings.TemperatureThreshold = settingsDto.TemperatureThreshold;
                    existingSettings.HumidityThreshold = settingsDto.HumidityThreshold;
                    existingSettings.DustThreshold = settingsDto.DustThreshold;
                    existingSettings.WaterLevelThreshold = settingsDto.WaterLevelThreshold;
                    existingSettings.FoodLevelThreshold = settingsDto.FoodLevelThreshold;
                    existingSettings.CheckInterval = settingsDto.CheckInterval;
                    existingSettings.LastUpdated = DateTime.UtcNow;

                    _context.NotificationSettings.Update(existingSettings);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Paramètres mis à jour pour {UserId}", settingsDto.UserId);
                    return MapToDto(existingSettings);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la sauvegarde des paramètres pour {UserId}", settingsDto.UserId);
                throw;
            }
        }

        /// <summary>
        /// Active ou désactive toutes les notifications pour un utilisateur
        /// </summary>
        /// <param name="userId">ID de l'utilisateur</param>
        /// <param name="enabled">État souhaité (true = activé, false = désactivé)</param>
        /// <returns>True si l'opération a réussi, false sinon</returns>
        public async Task<bool> ToggleNotificationsAsync(string userId, bool enabled)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("ToggleNotificationsAsync appelé avec userId vide");
                    return false;
                }

                var settings = await _context.NotificationSettings
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (settings == null)
                {
                    _logger.LogWarning("Paramètres non trouvés pour {UserId}", userId);
                    return false;
                }

                settings.NotificationsEnabled = enabled;
                settings.LastUpdated = DateTime.UtcNow;

                _context.NotificationSettings.Update(settings);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Notifications {Status} pour {UserId}", 
                    enabled ? "activées" : "désactivées", 
                    userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du toggle des notifications pour {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Met à jour uniquement les seuils de déclenchement des notifications
        /// </summary>
        /// <param name="userId">ID de l'utilisateur</param>
        /// <param name="settingsDto">DTO contenant les nouveaux seuils</param>
        /// <returns>True si l'opération a réussi, false sinon</returns>
        public async Task<bool> UpdateThresholdsAsync(string userId, NotificationSettings settingsDto)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("UpdateThresholdsAsync appelé avec userId vide");
                    return false;
                }

                var settings = await _context.NotificationSettings
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (settings == null)
                {
                    _logger.LogWarning("Paramètres non trouvés pour {UserId}", userId);
                    return false;
                }

                // Mettre à jour uniquement les seuils
                settings.TemperatureThreshold = settingsDto.TemperatureThreshold;
                settings.HumidityThreshold = settingsDto.HumidityThreshold;
                settings.DustThreshold = settingsDto.DustThreshold;
                settings.WaterLevelThreshold = settingsDto.WaterLevelThreshold;
                settings.FoodLevelThreshold = settingsDto.FoodLevelThreshold;
                settings.CheckInterval = settingsDto.CheckInterval;
                settings.LastUpdated = DateTime.UtcNow;

                _context.NotificationSettings.Update(settings);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Seuils mis à jour pour {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la mise à jour des seuils pour {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Supprime les paramètres de notification d'un utilisateur
        /// </summary>
        /// <param name="userId">ID de l'utilisateur</param>
        /// <returns>True si l'opération a réussi, false sinon</returns>
        public async Task<bool> DeleteSettingsAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("DeleteSettingsAsync appelé avec userId vide");
                    return false;
                }

                var settings = await _context.NotificationSettings
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (settings == null)
                {
                    _logger.LogWarning("Paramètres non trouvés pour {UserId}", userId);
                    return false;
                }

                _context.NotificationSettings.Remove(settings);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Paramètres supprimés pour {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression des paramètres pour {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Convertit une entité NotificationSettings en DTO
        /// </summary>
        /// <param name="settings">Entité à convertir</param>
        /// <returns>DTO correspondant</returns>
        private NotificationSettings MapToDto(NotificationSettings settings)
        {
            return new NotificationSettings
            {
                UserId = settings.UserId,
                NotificationsEnabled = settings.NotificationsEnabled,
                EmailNotifications = settings.EmailNotifications,
                PushNotifications = settings.PushNotifications,
                SmsNotifications = settings.SmsNotifications,
                TemperatureThreshold = settings.TemperatureThreshold,
                HumidityThreshold = settings.HumidityThreshold,
                DustThreshold = settings.DustThreshold,
                WaterLevelThreshold = settings.WaterLevelThreshold,
                FoodLevelThreshold = settings.FoodLevelThreshold,
                CheckInterval = settings.CheckInterval,
                LastUpdated = settings.LastUpdated
            };
        }
    }
    
}