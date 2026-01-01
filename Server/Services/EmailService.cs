// SmartNest.Server/Services/EmailService.cs
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace SmartNest.Server.Services
{
    /// <summary>
    /// Configuration pour le service d'email
    /// </summary>
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "SmartNest";
        public bool EnableSsl { get; set; } = true;
    }

    public interface IEmailService
    {
        Task<bool> SendNotificationEmailAsync(string toEmail, string subject, string message, string severity);
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
    }

    /// <summary>
    /// Service d'envoi d'emails pour les notifications
    /// Supporte SMTP (Gmail, Outlook, SendGrid, etc.)
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<EmailSettings> emailSettings,
            ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Envoie un email de notification formaté
        /// </summary>
        public async Task<bool> SendNotificationEmailAsync(
            string toEmail, 
            string subject, 
            string message, 
            string severity)
        {
            try
            {
                var htmlBody = GenerateNotificationEmailTemplate(subject, message, severity);
                return await SendEmailAsync(toEmail, $"🔔 {subject}", htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi de l'email de notification à {Email}", toEmail);
                return false;
            }
        }

        /// <summary>
        /// Envoie un email HTML
        /// </summary>
        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                // Validation des paramètres
                if (string.IsNullOrEmpty(toEmail) || 
                    string.IsNullOrEmpty(_emailSettings.SmtpHost) ||
                    string.IsNullOrEmpty(_emailSettings.SmtpUsername))
                {
                    _logger.LogWarning("Configuration email incomplète ou email destinataire vide");
                    return false;
                }

                using var smtpClient = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort)
                {
                    Credentials = new NetworkCredential(
                        _emailSettings.SmtpUsername, 
                        _emailSettings.SmtpPassword),
                    EnableSsl = _emailSettings.EnableSsl,
                    Timeout = 30000 // 30 secondes
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                    Priority = MailPriority.High
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Email envoyé avec succès à {Email}", toEmail);
                return true;
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "Erreur SMTP lors de l'envoi de l'email à {Email}", toEmail);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi de l'email à {Email}", toEmail);
                return false;
            }
        }

        /// <summary>
        /// Génère un template HTML pour les emails de notification
        /// </summary>
        private string GenerateNotificationEmailTemplate(string subject, string message, string severity)
        {
            var (color, icon, severityText) = severity switch
            {
                "Critical" => ("#e74c3c", "⚠️", "CRITIQUE"),
                "Warning" => ("#f39c12", "⚡", "AVERTISSEMENT"),
                "Info" => ("#3498db", "ℹ️", "INFORMATION"),
                "Success" => ("#06d6a0", "✅", "SUCCÈS"),
                _ => ("#6c757d", "🔔", "NOTIFICATION")
            };

            return $@"
<!DOCTYPE html>
<html lang='fr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>SmartNest - Notification</title>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f4f4f4; padding: 20px;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background-color: white; border-radius: 10px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                    <!-- En-tête -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; text-align: center;'>
                            <h1 style='color: white; margin: 0; font-size: 28px;'>
                                🐔 SmartNest
                            </h1>
                            <p style='color: rgba(255,255,255,0.9); margin: 5px 0 0 0; font-size: 14px;'>
                                Système de surveillance de poulailler
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Badge de sévérité -->
                    <tr>
                        <td style='padding: 20px; text-align: center;'>
                            <div style='display: inline-block; background-color: {color}; color: white; padding: 10px 20px; border-radius: 20px; font-weight: bold; font-size: 12px;'>
                                {icon} {severityText}
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Contenu -->
                    <tr>
                        <td style='padding: 0 30px 30px 30px;'>
                            <h2 style='color: #2d3748; margin: 0 0 15px 0; font-size: 22px;'>
                                {subject}
                            </h2>
                            <p style='color: #4a5568; line-height: 1.6; font-size: 16px; margin: 0;'>
                                {message}
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Bouton d'action -->
                    <tr>
                        <td style='padding: 0 30px 30px 30px; text-align: center;'>
                            <a href='https://votre-domaine.com/notifications' 
                               style='display: inline-block; background-color: #4361ee; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                                Voir les détails
                            </a>
                        </td>
                    </tr>
                    
                    <!-- Informations -->
                    <tr>
                        <td style='background-color: #f8f9fa; padding: 20px 30px; border-top: 1px solid #e9ecef;'>
                            <p style='color: #6c757d; font-size: 12px; margin: 0; line-height: 1.5;'>
                                📅 {DateTime.Now:dd/MM/yyyy HH:mm}<br>
                                Cette notification a été générée automatiquement par votre système SmartNest.
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Pied de page -->
                    <tr>
                        <td style='background-color: #2d3748; padding: 20px; text-align: center;'>
                            <p style='color: rgba(255,255,255,0.7); font-size: 12px; margin: 0;'>
                                © 2025 SmartNest - Tous droits réservés<br>
                                <a href='#' style='color: rgba(255,255,255,0.7); text-decoration: none;'>Se désabonner</a>
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }
    }
}