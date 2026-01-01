using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace SmartNest.Client.Services
{
    public class MqttConnectionService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthenticationStateProvider _authProvider;
        private readonly ILogger<MqttConnectionService> _logger;
        private bool _isConnected = false;
        private string? _currentUserId = null;

        public MqttConnectionService(
            HttpClient httpClient,
            AuthenticationStateProvider authProvider,
            ILogger<MqttConnectionService> logger)
        {
            _httpClient = httpClient;
            _authProvider = authProvider;
            _logger = logger;
        }

        public bool IsConnected => _isConnected;
        public string? CurrentUserId => _currentUserId;

        /// <summary>
        /// Initialise la connexion MQTT au début de la session
        /// </summary>
        public async Task<bool> InitializeSessionAsync(string? brokerUrl = null, int? port = null)
        {
            try
            {
                // Récupérer le userId de l'utilisateur authentifié
                _currentUserId = await GetCurrentUserIdAsync();

                _logger.LogInformation($"🔍 Retrieved userId: {_currentUserId}");

                if (_currentUserId == "anonymous")
                {
                    _logger.LogWarning("⚠️ User is not authenticated, skipping MQTT connection");
                    return false;
                }

                _logger.LogInformation($"🔄 Initializing MQTT session for user: {_currentUserId}");

                // Connexion au broker MQTT via l'API
                var response = await ConnectToMqttAsync(brokerUrl, port);

                if (response?.Success == true)
                {
                    _isConnected = true;
                    _logger.LogInformation($"✅ MQTT session initialized successfully for user: {_currentUserId}");
                    return true;
                }
                else
                {
                    _logger.LogError($"❌ Failed to initialize MQTT session: {response?.Message ?? "Unknown error"}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error initializing MQTT session");
                return false;
            }
        }

        /// <summary>
        /// Connecte l'utilisateur au broker MQTT
        /// </summary>
        private async Task<MqttConnectionResponse?> ConnectToMqttAsync(string? brokerUrl = null, int? port = null)
        {
            try
            {
                var request = new MqttConnectionRequest
                {
                    BrokerUrl = brokerUrl,
                    Port = port
                };

                _logger.LogInformation($"📤 Sending connection request to API: BrokerUrl={request.BrokerUrl}, Port={request.Port}");

                var response = await _httpClient.PostAsJsonAsync("api/MqttConnection/connect", request);

                _logger.LogInformation($"📥 API Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<MqttConnectionResponse>();
                    
                    if (result != null)
                    {
                        _logger.LogInformation($"✅ Connection successful: {result.Message}");
                        return result;
                    }
                    else
                    {
                        _logger.LogError("❌ Empty response from API");
                        return new MqttConnectionResponse { Success = false, Message = "Empty response from server" };
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ API Error ({response.StatusCode}): {errorContent}");
                    
                    return new MqttConnectionResponse
                    {
                        Success = false,
                        Message = $"HTTP {response.StatusCode}: {errorContent}"
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "❌ HTTP Request error connecting to MQTT broker");
                return new MqttConnectionResponse
                {
                    Success = false,
                    Message = $"Network error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unexpected error connecting to MQTT broker");
                return new MqttConnectionResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Déconnecte l'utilisateur du broker MQTT
        /// </summary>
        public async Task<bool> DisconnectAsync()
        {
            try
            {
                _logger.LogInformation("🔌 Requesting disconnection from MQTT broker");
                
                var response = await _httpClient.PostAsync("api/MqttConnection/disconnect", null);

                if (response.IsSuccessStatusCode)
                {
                    _isConnected = false;
                    _logger.LogInformation("✅ Disconnected from MQTT broker");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ Disconnection failed ({response.StatusCode}): {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error disconnecting from MQTT broker");
                return false;
            }
        }

        /// <summary>
        /// Récupère le statut de la connexion MQTT
        /// </summary>
        public async Task<MqttStatusResponse?> GetConnectionStatusAsync()
        {
            try
            {
                _logger.LogInformation("🔍 Checking MQTT connection status");
                
                var response = await _httpClient.GetFromJsonAsync<MqttStatusResponse>("api/MqttConnection/status");
                
                if (response != null)
                {
                    _isConnected = response.IsConnected;
                    _logger.LogInformation($"📊 Status: Connected={response.IsConnected}, CurrentUserId={response.CurrentUserId}");
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting MQTT connection status");
                return null;
            }
        }

        /// <summary>
        /// Envoie la configuration au ESP32 spécifié
        /// </summary>
        public async Task<bool> SendConfigToESP32Async(string macAddress)
        {
            try
            {
                _logger.LogInformation($"📤 Sending config to ESP32: {macAddress}");
                
                var response = await _httpClient.PostAsync($"api/MqttConnection/send-config/{macAddress}", null);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Configuration sent to ESP32: {macAddress}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ Failed to send config ({response.StatusCode}): {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error sending config to ESP32: {macAddress}");
                return false;
            }
        }

        /// <summary>
        /// Récupère le userId de l'utilisateur authentifié
        /// IMPORTANT: Doit correspondre exactement aux claims utilisés par le backend
        /// </summary>
        private async Task<string> GetCurrentUserIdAsync()
        {
            try
            {
                var authState = await _authProvider.GetAuthenticationStateAsync();
                var user = authState.User;

                if (user?.Identity?.IsAuthenticated == true)
                {
                    // Essayer tous les claims possibles dans l'ordre de priorité
                    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value  // Standard ASP.NET
                              ?? user.FindFirst("sub")?.Value                       // JWT standard
                              ?? user.FindFirst("oid")?.Value                       // Azure AD
                              ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                              ?? "anonymous";

                    _logger.LogInformation($"🔑 UserId extracted from claims: {userId}");
                    
                    // Déboguer tous les claims disponibles
                    _logger.LogInformation("📋 Available claims:");
                    foreach (var claim in user.Claims)
                    {
                        _logger.LogInformation($"   - {claim.Type}: {claim.Value}");
                    }

                    return userId;
                }

                _logger.LogWarning("⚠️ User is not authenticated");
                return "anonymous";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error extracting userId from claims");
                return "anonymous";
            }
        }

        /// <summary>
        /// Vérifie si le service est prêt et l'utilisateur authentifié
        /// </summary>
        public async Task<bool> IsReadyAsync()
        {
            var userId = await GetCurrentUserIdAsync();
            return userId != "anonymous";
        }
    }

    // DTOs - DOIVENT correspondre exactement aux réponses du backend
    public class MqttConnectionRequest
    {
        public string? BrokerUrl { get; set; }
        public int? Port { get; set; }
    }

    public class MqttConnectionResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? UserId { get; set; }
        public bool IsConnected { get; set; }
    }

    public class MqttStatusResponse
    {
        public bool IsConnected { get; set; }
        public string? CurrentUserId { get; set; }
        public string? UserId { get; set; }
    }
}