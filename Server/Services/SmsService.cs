// SmartNest.Server/Services/SmsService.cs
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SmartNest.Server.Services
{
    /// <summary>
    /// Configuration pour le service SMS
    /// Supporte Twilio, Vonage (Nexmo), AWS SNS, etc.
    /// </summary>
    public class SmsSettings
    {
        public string Provider { get; set; } = "Twilio"; // Twilio, Vonage, AWS_SNS
        
        // Configuration Twilio
        public string TwilioAccountSid { get; set; } = string.Empty;
        public string TwilioAuthToken { get; set; } = string.Empty;
        public string TwilioPhoneNumber { get; set; } = string.Empty;
        
        // Configuration Vonage (Nexmo)
        public string VonageApiKey { get; set; } = string.Empty;
        public string VonageApiSecret { get; set; } = string.Empty;
        public string VonageFromNumber { get; set; } = string.Empty;
        
        // Configuration AWS SNS
        public string AwsAccessKeyId { get; set; } = string.Empty;
        public string AwsSecretAccessKey { get; set; } = string.Empty;
        public string AwsRegion { get; set; } = "us-east-1";
        
        // Options générales
        public bool EnableSms { get; set; } = false;
        public int MaxMessageLength { get; set; } = 160;
    }

    public interface ISmsService
    {
        Task<bool> SendNotificationSmsAsync(string phoneNumber, string message, string severity);
        Task<bool> SendSmsAsync(string phoneNumber, string message);
    }

    /// <summary>
    /// Service d'envoi de SMS pour les notifications critiques
    /// Support multi-provider (Twilio, Vonage, AWS SNS)
    /// </summary>
    public class SmsService : ISmsService
    {
        private readonly SmsSettings _smsSettings;
        private readonly ILogger<SmsService> _logger;
        private readonly HttpClient _httpClient;

        public SmsService(
            IOptions<SmsSettings> smsSettings,
            ILogger<SmsService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _smsSettings = smsSettings.Value;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        /// <summary>
        /// Envoie un SMS de notification formaté
        /// </summary>
        public async Task<bool> SendNotificationSmsAsync(
            string phoneNumber, 
            string message, 
            string severity)
        {
            try
            {
                if (!_smsSettings.EnableSms)
                {
                    _logger.LogWarning("L'envoi de SMS est désactivé dans la configuration");
                    return false;
                }

                var icon = severity switch
                {
                    "Critical" => "🚨",
                    "Warning" => "⚠️",
                    "Info" => "ℹ️",
                    "Success" => "✅",
                    _ => "🔔"
                };

                var formattedMessage = $"{icon} SmartNest: {message}";
                
                // Tronquer si trop long
                if (formattedMessage.Length > _smsSettings.MaxMessageLength)
                {
                    formattedMessage = formattedMessage.Substring(0, _smsSettings.MaxMessageLength - 3) + "...";
                }

                return await SendSmsAsync(phoneNumber, formattedMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi du SMS de notification à {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        /// <summary>
        /// Envoie un SMS via le provider configuré
        /// </summary>
        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                if (!_smsSettings.EnableSms)
                {
                    _logger.LogWarning("L'envoi de SMS est désactivé");
                    return false;
                }

                // Nettoyer le numéro de téléphone
                phoneNumber = CleanPhoneNumber(phoneNumber);

                if (string.IsNullOrEmpty(phoneNumber))
                {
                    _logger.LogWarning("Numéro de téléphone invalide");
                    return false;
                }

                return _smsSettings.Provider.ToLower() switch
                {
                    "twilio" => await SendViaTwilioAsync(phoneNumber, message),
                    "vonage" => await SendViaVonageAsync(phoneNumber, message),
                    "aws_sns" => await SendViaAwsSnsAsync(phoneNumber, message),
                    _ => throw new NotSupportedException($"Provider SMS '{_smsSettings.Provider}' non supporté")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi du SMS à {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        /// <summary>
        /// Envoie un SMS via Twilio
        /// Documentation: https://www.twilio.com/docs/sms/api
        /// </summary>
        private async Task<bool> SendViaTwilioAsync(string phoneNumber, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_smsSettings.TwilioAccountSid) || 
                    string.IsNullOrEmpty(_smsSettings.TwilioAuthToken))
                {
                    _logger.LogWarning("Configuration Twilio incomplète");
                    return false;
                }

                var url = $"https://api.twilio.com/2010-04-01/Accounts/{_smsSettings.TwilioAccountSid}/Messages.json";

                var credentials = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{_smsSettings.TwilioAccountSid}:{_smsSettings.TwilioAuthToken}"));

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("To", phoneNumber),
                    new KeyValuePair<string, string>("From", _smsSettings.TwilioPhoneNumber),
                    new KeyValuePair<string, string>("Body", message)
                });

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("SMS Twilio envoyé avec succès à {PhoneNumber}", phoneNumber);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erreur Twilio: {StatusCode} - {Error}", 
                        response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi via Twilio");
                return false;
            }
        }

        /// <summary>
        /// Envoie un SMS via Vonage (Nexmo)
        /// Documentation: https://developer.vonage.com/api/sms
        /// </summary>
        private async Task<bool> SendViaVonageAsync(string phoneNumber, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_smsSettings.VonageApiKey) || 
                    string.IsNullOrEmpty(_smsSettings.VonageApiSecret))
                {
                    _logger.LogWarning("Configuration Vonage incomplète");
                    return false;
                }

                var url = "https://rest.nexmo.com/sms/json";

                var payload = new
                {
                    api_key = _smsSettings.VonageApiKey,
                    api_secret = _smsSettings.VonageApiSecret,
                    to = phoneNumber,
                    from = _smsSettings.VonageFromNumber,
                    text = message
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload), 
                    Encoding.UTF8, 
                    "application/json");

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<VonageResponse>();
                    
                    if (result?.messages?.FirstOrDefault()?.status == "0")
                    {
                        _logger.LogInformation("SMS Vonage envoyé avec succès à {PhoneNumber}", phoneNumber);
                        return true;
                    }
                    else
                    {
                        _logger.LogError("Erreur Vonage: {Error}", 
                            result?.messages?.FirstOrDefault()?.error_text ?? "Unknown error");
                        return false;
                    }
                }
                else
                {
                    _logger.LogError("Erreur Vonage HTTP: {StatusCode}", response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi via Vonage");
                return false;
            }
        }

        /// <summary>
        /// Envoie un SMS via AWS SNS
        /// Documentation: https://docs.aws.amazon.com/sns/latest/dg/sms_publish-to-phone.html
        /// Note: Nécessite le package AWSSDK.SimpleNotificationService
        /// </summary>
        private async Task<bool> SendViaAwsSnsAsync(string phoneNumber, string message)
        {
            try
            {
                _logger.LogWarning("AWS SNS n'est pas encore implémenté. Veuillez installer AWSSDK.SimpleNotificationService");
                
                // Pour implémenter AWS SNS, installez le package:
                // dotnet add package AWSSDK.SimpleNotificationService
                
                // Puis utilisez:
                /*
                using Amazon;
                using Amazon.SimpleNotificationService;
                using Amazon.SimpleNotificationService.Model;

                var snsClient = new AmazonSimpleNotificationServiceClient(
                    _smsSettings.AwsAccessKeyId,
                    _smsSettings.AwsSecretAccessKey,
                    RegionEndpoint.GetBySystemName(_smsSettings.AwsRegion));

                var request = new PublishRequest
                {
                    Message = message,
                    PhoneNumber = phoneNumber,
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        { "AWS.SNS.SMS.SMSType", new MessageAttributeValue 
                            { DataType = "String", StringValue = "Transactional" } }
                    }
                };

                var response = await snsClient.PublishAsync(request);
                return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
                */

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi via AWS SNS");
                return false;
            }
        }

        /// <summary>
        /// Nettoie et formate un numéro de téléphone
        /// </summary>
        private string CleanPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return string.Empty;

            // Supprimer les espaces, tirets, parenthèses
            phoneNumber = phoneNumber.Replace(" ", "")
                                   .Replace("-", "")
                                   .Replace("(", "")
                                   .Replace(")", "");

            // Ajouter le + si manquant
            if (!phoneNumber.StartsWith("+"))
            {
                // Si commence par 00, remplacer par +
                if (phoneNumber.StartsWith("00"))
                    phoneNumber = "+" + phoneNumber.Substring(2);
                // Sinon, assumer format international (+221 pour Sénégal)
                else if (!phoneNumber.StartsWith("0"))
                    phoneNumber = "+221" + phoneNumber;
            }

            return phoneNumber;
        }

        // Classes pour la désérialisation des réponses
        private class VonageResponse
        {
            public List<VonageMessage>? messages { get; set; }
        }

        private class VonageMessage
        {
            public string? status { get; set; }
            public string? error_text { get; set; }
        }
    }
}