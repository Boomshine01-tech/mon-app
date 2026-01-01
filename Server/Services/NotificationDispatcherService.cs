// SmartNest.Server/Services/NotificationDispatcherService.cs
using Microsoft.EntityFrameworkCore;
using SmartNest.Server.Data;
using SmartNest.Server.Models.postgres;

namespace SmartNest.Server.Services
{
    /// <summary>
    /// Service orchestrateur qui dispatche les notifications vers les bons canaux
    /// (Email, SMS, Push) selon les préférences utilisateur
    /// </summary>
    public interface INotificationDispatcherService
    {
        Task DispatchNotificationAsync(CreateNotificationRequest notification);
        Task DispatchNotificationToUserAsync(string userId, CreateNotificationRequest notification);
    }

    public class NotificationDispatcherService : INotificationDispatcherService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationSettingsService _settingsService;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly ILogger<NotificationDispatcherService> _logger;

        public NotificationDispatcherService(
            ApplicationDbContext context,
            INotificationSettingsService settingsService,
            IEmailService emailService,
            ISmsService smsService,
            ILogger<NotificationDispatcherService> logger)
        {
            _context = context;
            _settingsService = settingsService;
            _emailService = emailService;
            _smsService = smsService;
            _logger = logger;
        }

        /// <summary>
        /// Dispatche une notification vers les canaux appropriés
        /// </summary>
        public async Task DispatchNotificationAsync(CreateNotificationRequest notification)
        {
            await DispatchNotificationToUserAsync(notification.UserId, notification);
        }

        /// <summary>
        /// Dispatche une notification à un utilisateur spécifique
        /// selon ses préférences de notification
        /// </summary>
        public async Task DispatchNotificationToUserAsync(
            string userId, 
            CreateNotificationRequest notification)
        {
            try
            {
                // Récupérer les paramètres de notification de l'utilisateur
                var settings = await _settingsService.GetSettingsAsync(userId);

                if (settings == null || !settings.NotificationsEnabled)
                {
                    _logger.LogInformation(
                        "Notifications désactivées pour l'utilisateur {UserId}", 
                        userId);
                    return;
                }

                // Récupérer les informations de l'utilisateur
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    _logger.LogWarning("Utilisateur {UserId} non trouvé", userId);
                    return;
                }

                // Dispatcher vers les différents canaux en parallèle
                var tasks = new List<Task>();

                // Email
                if (settings.EmailNotifications && !string.IsNullOrEmpty(user.Email))
                {
                    tasks.Add(SendEmailNotificationAsync(user.Email, notification));
                }

                // SMS (uniquement pour les notifications critiques)
                if (settings.SmsNotifications && 
                    notification.Severity == "Critical" && 
                    !string.IsNullOrEmpty(user.PhoneNumber))
                {
                    tasks.Add(SendSmsNotificationAsync(user.PhoneNumber, notification));
                }

                // Push notifications (si implémenté)
                if (settings.PushNotifications)
                {
                    // TODO: Implémenter les push notifications
                    _logger.LogInformation("Push notification pour {UserId} (non implémenté)", userId);
                }

                // Attendre l'envoi de toutes les notifications
                await Task.WhenAll(tasks);

                _logger.LogInformation(
                    "Notification dispatchée pour {UserId} via {Channels} canaux", 
                    userId, 
                    tasks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Erreur lors du dispatch de notification pour {UserId}", 
                    userId);
            }
        }

        /// <summary>
        /// Envoie une notification par email
        /// </summary>
        private async Task SendEmailNotificationAsync(
            string email, 
            CreateNotificationRequest notification)
        {
            try
            {
                var success = await _emailService.SendNotificationEmailAsync(
                    email,
                    notification.Title,
                    notification.Message,
                    notification.Severity);

                if (success)
                {
                    _logger.LogInformation(
                        "Email de notification envoyé à {Email} - {Title}", 
                        email, 
                        notification.Title);
                }
                else
                {
                    _logger.LogWarning(
                        "Échec de l'envoi d'email à {Email}", 
                        email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi d'email à {Email}", email);
            }
        }

        /// <summary>
        /// Envoie une notification par SMS
        /// </summary>
        private async Task SendSmsNotificationAsync(
            string phoneNumber, 
            CreateNotificationRequest notification)
        {
            try
            {
                var success = await _smsService.SendNotificationSmsAsync(
                    phoneNumber,
                    notification.Message,
                    notification.Severity);

                if (success)
                {
                    _logger.LogInformation(
                        "SMS de notification envoyé à {PhoneNumber} - {Title}", 
                        phoneNumber, 
                        notification.Title);
                }
                else
                {
                    _logger.LogWarning(
                        "Échec de l'envoi de SMS à {PhoneNumber}", 
                        phoneNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi de SMS à {PhoneNumber}", phoneNumber);
            }
        }
    }
}