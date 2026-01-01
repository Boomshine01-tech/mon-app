// SmartNest.Server/Controllers/NotificationsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartNest.Server.Services;
using SmartNest.Server.Models.postgres;

namespace SmartNest.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly INotificationSettingsService _settingsService;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            INotificationService notificationService,
            INotificationSettingsService settingsService,
            ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _settingsService = settingsService;
            _logger = logger;
        }

        // GET: api/notifications/{userId}
        [HttpGet("{userId}")]
        public async Task<ActionResult<List<Notification>>> GetUserNotifications(string userId)
        {
            try
            {
                var notifications = await _notificationService.GetUserNotificationsAsync(userId);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des notifications pour {UserId}", userId);
                return StatusCode(500, "Erreur lors de la récupération des notifications");
            }
        }

        // GET: api/notifications/{notificationId}/details/{userId}
        [HttpGet("{notificationId}/details/{userId}")]
        public async Task<ActionResult<Notification>> GetNotificationById(string notificationId, string userId)
        {
            try
            {
                var notification = await _notificationService.GetNotificationByIdAsync(notificationId, userId);
                
                if (notification == null)
                    return NotFound("Notification non trouvée");

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de la notification {NotificationId}", notificationId);
                return StatusCode(500, "Erreur lors de la récupération de la notification");
            }
        }

        // POST: api/notifications
        [HttpPost]
        public async Task<ActionResult<Notification>> CreateNotification([FromBody] CreateNotificationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId) || 
                    string.IsNullOrEmpty(request.Title) || 
                    string.IsNullOrEmpty(request.Message))
                {
                    return BadRequest("UserId, Title et Message sont requis");
                }

                var notification = await _notificationService.CreateNotificationAsync(request);
                return CreatedAtAction(nameof(GetNotificationById), 
                    new { notificationId = notification.Id, userId = notification.UserId }, 
                    notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création de la notification");
                return StatusCode(500, "Erreur lors de la création de la notification");
            }
        }

        // POST: api/notifications/{notificationId}/mark-read
        [HttpPost("{notificationId}/mark-read")]
        public async Task<ActionResult> MarkAsRead(string notificationId, [FromQuery] string userId)
        {
            try
            {
                var success = await _notificationService.MarkAsReadAsync(notificationId, userId);
                
                if (!success)
                    return NotFound("Notification non trouvée");

                return Ok(new { message = "Notification marquée comme lue" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du marquage comme lu");
                return StatusCode(500, "Erreur lors du marquage comme lu");
            }
        }

        // POST: api/notifications/{userId}/mark-all-read
        [HttpPost("{userId}/mark-all-read")]
        public async Task<ActionResult> MarkAllAsRead(string userId)
        {
            try
            {
                await _notificationService.MarkAllAsReadAsync(userId);
                return Ok(new { message = "Toutes les notifications marquées comme lues" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du marquage de toutes les notifications");
                return StatusCode(500, "Erreur lors du marquage de toutes les notifications");
            }
        }

        // DELETE: api/notifications/{notificationId}
        [HttpDelete("{notificationId}")]
        public async Task<ActionResult> DeleteNotification(string notificationId, [FromQuery] string userId)
        {
            try
            {
                var success = await _notificationService.DeleteNotificationAsync(notificationId, userId);
                
                if (!success)
                    return NotFound("Notification non trouvée");

                return Ok(new { message = "Notification supprimée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression");
                return StatusCode(500, "Erreur lors de la suppression");
            }
        }

        // GET: api/notifications/{userId}/unread-count
        [HttpGet("{userId}/unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount(string userId)
        {
            try
            {
                var count = await _notificationService.GetUnreadCountAsync(userId);
                return Ok(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du comptage des notifications non lues");
                return StatusCode(500, "Erreur lors du comptage");
            }
        }

        // GET: api/notifications/{userId}/category/{category}
        [HttpGet("{userId}/category/{category}")]
        public async Task<ActionResult<List<Notification>>> GetByCategory(string userId, string category)
        {
            try
            {
                var notifications = await _notificationService.GetNotificationsByCategoryAsync(userId, category);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération par catégorie");
                return StatusCode(500, "Erreur lors de la récupération");
            }
        }

        // GET: api/notifications/{userId}/severity/{severity}
        [HttpGet("{userId}/severity/{severity}")]
        public async Task<ActionResult<List<Notification>>> GetBySeverity(string userId, string severity)
        {
            try
            {
                var notifications = await _notificationService.GetNotificationsBySeverityAsync(userId, severity);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération par sévérité");
                return StatusCode(500, "Erreur lors de la récupération");
            }
        }

        // DELETE: api/notifications/{userId}/old
        [HttpDelete("{userId}/old")]
        public async Task<ActionResult> DeleteOldNotifications(string userId, [FromQuery] int daysOld = 30)
        {
            try
            {
                await _notificationService.DeleteOldNotificationsAsync(userId, daysOld);
                return Ok(new { message = $"Notifications de plus de {daysOld} jours supprimées" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression des anciennes notifications");
                return StatusCode(500, "Erreur lors de la suppression");
            }
        }

        // ===== SETTINGS ENDPOINTS =====

        // GET: api/notifications/settings/{userId}
        [HttpGet("settings/{userId}")]
        public async Task<ActionResult<NotificationSettings>> GetSettings(string userId)
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync(userId);
                
                if (settings == null)
                    return NotFound("Paramètres non trouvés");

                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des paramètres");
                return StatusCode(500, "Erreur lors de la récupération des paramètres");
            }
        }

        // POST: api/notifications/settings
        [HttpPost("settings")]
        public async Task<ActionResult<NotificationSettings>> SaveSettings([FromBody] NotificationSettings settings)
        {
            try
            {
                if (string.IsNullOrEmpty(settings.UserId))
                    return BadRequest("UserId est requis");

                var savedSettings = await _settingsService.CreateOrUpdateSettingsAsync(settings);
                return Ok(savedSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la sauvegarde des paramètres");
                return StatusCode(500, "Erreur lors de la sauvegarde des paramètres");
            }
        }

        // POST: api/notifications/settings/{userId}/toggle
        [HttpPost("settings/{userId}/toggle")]
        public async Task<ActionResult> ToggleNotifications(string userId, [FromBody] bool enabled)
        {
            try
            {
                var success = await _settingsService.ToggleNotificationsAsync(userId, enabled);
                
                if (!success)
                    return NotFound("Paramètres non trouvés");

                return Ok(new { message = $"Notifications {(enabled ? "activées" : "désactivées")}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du toggle des notifications");
                return StatusCode(500, "Erreur lors du toggle");
            }
        }

        // PUT: api/notifications/settings/{userId}/thresholds
        [HttpPut("settings/{userId}/thresholds")]
        public async Task<ActionResult> UpdateThresholds(string userId, [FromBody] NotificationSettings settings)
        {
            try
            {
                var success = await _settingsService.UpdateThresholdsAsync(userId, settings);
                
                if (!success)
                    return NotFound("Paramètres non trouvés");

                return Ok(new { message = "Seuils mis à jour" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la mise à jour des seuils");
                return StatusCode(500, "Erreur lors de la mise à jour");
            }
        }
    }
}