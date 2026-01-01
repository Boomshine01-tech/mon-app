using MQTTnet;
using MQTTnet.Client;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SmartNest.Server.Hubs;
using SmartNest.Server.Models.postgres;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartNest.Server.Services
{
    public class MqttService
    {
        private readonly IMqttClient _mqttClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MqttService> _logger;
        private MqttClientOptions? _currentOptions;
        private string _currentUserId = string.Empty;

        public MqttService(IServiceProvider serviceProvider, ILogger<MqttService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _mqttClient = new MqttFactory().CreateMqttClient();
            ConfigureEventHandlers();
        }

        private void ConfigureEventHandlers()
        {
            _mqttClient.ConnectedAsync += async e =>
            {
                _logger.LogInformation("✅ Connected to MQTT broker");
                
                if (!string.IsNullOrEmpty(_currentUserId))
                {
                    await SendUserIdToESP32(_currentUserId);
                }
                
                await Task.CompletedTask;
            };

            _mqttClient.DisconnectedAsync += async e =>
            {
                _logger.LogWarning("⚠️ Disconnected from MQTT broker. Reconnecting in 5 seconds...");
                await Task.Delay(5000);
                
                if (_currentOptions != null)
                {
                    try
                    {
                        await _mqttClient.ConnectAsync(_currentOptions);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reconnect to MQTT broker");
                    }
                }
            };

            _mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var message = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                var topic = e.ApplicationMessage.Topic;
                
                _logger.LogInformation($"📩 MQTT Message - Topic: {topic}, Payload: {message}");
                
                await ProcessMessage(topic, message);
            };
        }

        /// <summary>
        /// Connecte un utilisateur authentifié au broker MQTT
        /// L'userId doit être le véritable ID issu de l'authentification (via les claims JWT)
        /// </summary>
        public async Task ConnectUserBroker(string userId, string brokerUrl = "192.168.1.24", int port = 1883)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
                }

                if (userId == "anonymous")
                {
                    throw new ArgumentException("Cannot connect with anonymous user ID", nameof(userId));
                }

                // Si déjà connecté avec un utilisateur différent, déconnecter d'abord
                if (_mqttClient.IsConnected && !string.IsNullOrEmpty(_currentUserId) && _currentUserId != userId)
                {
                    _logger.LogInformation($"🔄 Switching from user {_currentUserId} to {userId}");
                    await Disconnect();
                }

                _currentUserId = userId;
                _currentOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(brokerUrl, port)
                    .WithClientId($"Server_{userId}_{Guid.NewGuid()}")
                    .WithCleanSession()
                    .WithTimeout(TimeSpan.FromSeconds(5))
                    .Build();

                _logger.LogInformation($"🔄 Connecting authenticated user {userId} to MQTT broker at {brokerUrl}:{port}...");
                
                await _mqttClient.ConnectAsync(_currentOptions);

                // S'abonner aux topics spécifiques à cet utilisateur authentifié
                await _mqttClient.SubscribeAsync($"poultry/{userId}/+");
                await _mqttClient.SubscribeAsync($"devices/{userId}/+/status");
                await _mqttClient.SubscribeAsync($"esp32/register");
                await _mqttClient.SubscribeAsync($"esp32/+/ready");
                
                _logger.LogInformation($"✅ Authenticated user {userId} connected to MQTT broker");
                _logger.LogInformation($"📡 Subscribed to topics: poultry/{userId}/+, devices/{userId}/+/status");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to connect user {userId} to MQTT broker");
                throw;
            }
        }

        private async Task SendUserIdToESP32(string userId)
        {
            try
            {
                var broadcastTopic = "esp32/config/all";
                
                var configMessage = new
                {
                    userId = userId,
                    timestamp = DateTime.UtcNow,
                    action = "set_user_id"
                };

                var payload = JsonSerializer.Serialize(configMessage);

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(broadcastTopic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(true)
                    .Build();

                await _mqttClient.PublishAsync(message);
                
                _logger.LogInformation($"📤 User ID sent to ESP32 - Topic: {broadcastTopic}, UserId: {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to send user ID to ESP32");
            }
        }

        public async Task SendUserIdToSpecificESP32(string macAddress, string userId)
        {
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    _logger.LogWarning("⚠️ MQTT client not connected");
                    return;
                }

                var topic = $"esp32/config/{macAddress}";
                
                var configMessage = new
                {
                    userId = userId,
                    timestamp = DateTime.UtcNow,
                    action = "set_user_id",
                    macAddress = macAddress
                };

                var payload = JsonSerializer.Serialize(configMessage);

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                    .WithRetainFlag(false)
                    .Build();

                await _mqttClient.PublishAsync(message);
                
                _logger.LogInformation($"📤 User ID sent to specific ESP32 - MAC: {macAddress}, UserId: {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to send user ID to ESP32 {macAddress}");
            }
        }

        private async Task ProcessMessage(string topic, string message)
        {
            using var scope = _serviceProvider.CreateScope();
            var DbContext = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<RealtimeHub>>();

            try
            {
                // 🔹 Traitement des enregistrements d'ESP32
                if (topic == "esp32/register" || topic.StartsWith("esp32/") && topic.EndsWith("/ready"))
                {
                    await ProcessESP32Registration(topic, message);
                    return;
                }

                // 🔹 Extraire l'userId du topic - IMPORTANT: Utiliser le vrai userId
                var userIdFromTopic = ExtractUserIdFromTopic(topic);
                
                // 🔹 SÉCURITÉ: Vérifier que l'userId du topic correspond à l'userId connecté
                if (!string.IsNullOrEmpty(_currentUserId) && userIdFromTopic != _currentUserId)
                {
                    _logger.LogWarning($"⚠️ Security: Received message for user {userIdFromTopic} but connected as {_currentUserId}. Ignoring.");
                    return;
                }

                var userId = _currentUserId; // Utiliser le vrai userId authentifié

                // 🔹 Traitement des statuts d'appareils
                if (topic.Contains("/status"))
                {
                    await ProcessDeviceStatus(topic, message, DbContext, hubContext, userId);
                    return;
                }

                // 🔹 Traitement des données de capteurs
                _logger.LogInformation($"Processing sensor data for authenticated user {userId}: {topic}");

                // Parser le JSON pour extraire les valeurs des capteurs
                SensorPayload? sensorPayload = null;
                try
                {
                    sensorPayload = JsonSerializer.Deserialize<SensorPayload>(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Failed to parse sensor JSON: {message}");
                    // Fallback: sauvegarder dans payload seulement si le parsing échoue
                    var fallbackData = new Models.postgres.Sensordatum
                    {
                        userid = userId, // Utiliser le vrai userId authentifié
                        topic = topic,
                        payload = message,
                        timestamp = DateTime.UtcNow
                    };
                    await DbContext.SensorData.AddAsync(fallbackData);
                    await DbContext.SaveChangesAsync();
                    return;
                }

                // Créer l'objet avec les valeurs individuelles et le vrai userId
                var sensorData = new Models.postgres.Sensordatum
                {
                    userid = userId, // IMPORTANT: Utiliser le vrai userId authentifié, pas celui du topic
                    topic = topic,
                    payload = message,
                    timestamp = DateTime.UtcNow,
                    temperature = sensorPayload!.Temperature,
                    humidity = sensorPayload.Humidity,
                    dust = sensorPayload.Dust
                };

                await DbContext.SensorData.AddAsync(sensorData);
                await DbContext.SaveChangesAsync();

                // Notifier les clients via SignalR avec les valeurs individuelles
                await hubContext.Clients.Group(userId)
                    .SendAsync("SensorDataReceived", new {
                        topic = topic,
                        temperature = sensorData.temperature,
                        humidity = sensorData.humidity,
                        dust = sensorData.dust,
                        timestamp = sensorData.timestamp
                    });
                
                _logger.LogInformation($"✅ Sensor data saved for user {userId} - Temp: {sensorData.temperature}°C, Humidity: {sensorData.humidity}%, Dust: {sensorData.dust}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error processing MQTT message - Topic: {topic}, Message: {message}");
            }
        }

        private async Task ProcessESP32Registration(string topic, string message)
        {
            try
            {
                var registrationData = JsonSerializer.Deserialize<ESP32RegistrationMessage>(message);
                
                if (registrationData != null && !string.IsNullOrEmpty(registrationData.MacAddress))
                {
                    _logger.LogInformation($"🆕 New ESP32 registration - MAC: {registrationData.MacAddress}");
                    
                    if (!string.IsNullOrEmpty(_currentUserId))
                    {
                        await SendUserIdToSpecificESP32(registrationData.MacAddress, _currentUserId);
                        _logger.LogInformation($"✅ User ID {_currentUserId} sent to newly registered ESP32 {registrationData.MacAddress}");
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ Cannot send user ID to ESP32 {registrationData.MacAddress} - No authenticated user connected");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing ESP32 registration");
            }
        }

        private async Task ProcessDeviceStatus(string topic, string message, 
                                               Data.ApplicationDbContext DbContext,
                                               IHubContext<RealtimeHub> hubContext, 
                                               string userId)
        {
            try
            {
                var parts = topic.Split('/');
                if (parts.Length >= 4)
                {
                    var deviceId = parts[2];
    
                    _logger.LogInformation($"📍 Processing device status - DeviceId: {deviceId}, Authenticated UserId: {userId}");

                    DeviceStatusMessage? statusData = null;
                    try
                    {
                        statusData = JsonSerializer.Deserialize<DeviceStatusMessage>(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"❌ Failed to parse device status JSON: {message}");
                        return;
                    }
    
                    if (statusData != null)
                    {
                        // 🔒 Utiliser une stratégie de retry avec ExecuteInTransaction
                        await using var transaction = await  DbContext.Database.BeginTransactionAsync();
                
                        try
                        {
                            // Recharger l'appareil depuis la DB (pas depuis le cache)
                            var device = await DbContext.Devices
                                .Where(d => d.DeviceId == deviceId && d.UserId == userId)
                                .FirstOrDefaultAsync();

                            if (device != null)
                            {
                                _logger.LogInformation($"✅ Device found: {device.DeviceName}");
                
                                device.IsActive = statusData.IsActive;
                                device.LastUpdated = DateTime.UtcNow;
                                device.StatusMessage = statusData.IsActive ? "Appareil actif" : "Appareil inactif";

                                await DbContext.SaveChangesAsync();
                                await transaction.CommitAsync();
                        
                                _logger.LogInformation($"💾 Device {deviceId} updated for user {userId}");

                                await hubContext.Clients.Group(userId)
                                    .SendAsync("DeviceStatusChanged", new {
                                        deviceId = device.DeviceId,
                                        deviceName = device.DeviceName,
                                        deviceType = device.DeviceType,
                                        isActive = device.IsActive,
                                        statusMessage = device.StatusMessage,
                                        lastUpdated = device.LastUpdated
                                    });
                    
                                _logger.LogInformation($"📡 Status broadcasted to user {userId}");
                            }
                            else
                            {
                                await transaction.CommitAsync();
                        
                                _logger.LogWarning($"⚠️ Device {deviceId} not found for authenticated user {userId}");
                                _logger.LogInformation($"💡 Creating device {deviceId} for user {userId}...");
                
                                await CreateDeviceIfNotExists(DbContext, hubContext, deviceId, userId, statusData);
                            }
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogWarning($"⚠️ Concurrency conflict for device {deviceId}, ignoring duplicate update");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error processing device status - Topic: {topic}");
            }
        }

        private string ExtractDeviceTypeFromId(string deviceId)
        {
            if (deviceId.StartsWith("fan-")) return "Fan";
            if (deviceId.StartsWith("heatlamp-")) return "HeatLamp";
            if (deviceId.StartsWith("feeder-")) return "Feeder";
            if (deviceId.StartsWith("water-")) return "WaterDispenser";
            return "Unknown";
        }

        private async Task CreateDeviceIfNotExists(Data.ApplicationDbContext DbContext,
                                                   IHubContext<RealtimeHub> hubContext,
                                                   string deviceId, 
                                                   string userId, 
                                                   DeviceStatusMessage statusData)
        {
            try
            {
                var deviceType = ExtractDeviceTypeFromId(deviceId);
                var deviceName = deviceType switch
                {
                    "Fan" => "Ventilateur Principal",
                    "HeatLamp" => "Lampe Chauffante",
                    "Feeder" => "Distributeur d'Aliment",
                    "WaterDispenser" => "Distributeur d'Eau",
                    _ => "Appareil Inconnu"
                };

                var newDevice = new Models.postgres.device
                {
                    DeviceId = deviceId,
                    DeviceName = deviceName,
                    DeviceType = deviceType,
                    UserId = userId, // Utiliser le vrai userId authentifié
                    IsActive = statusData.IsActive,
                    LastUpdated = DateTime.UtcNow,
                    StatusMessage = statusData.IsActive ? "Appareil actif" : "Appareil inactif"
                };

                await DbContext.Devices.AddAsync(newDevice);
                await DbContext.SaveChangesAsync();

                await hubContext.Clients.Group(userId)
                    .SendAsync("DeviceStatusChanged", new {
                        deviceId = newDevice.DeviceId,
                        deviceName = newDevice.DeviceName,
                        deviceType = newDevice.DeviceType,
                        isActive = newDevice.IsActive,
                        statusMessage = newDevice.StatusMessage,
                        lastUpdated = newDevice.LastUpdated
                    });

                _logger.LogInformation($"✅ Device {deviceId} created for authenticated user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to auto-create device {deviceId}");
            }
        }

        public async Task<bool> PublishDeviceCommand(string userId, string deviceId, bool activate)
        {
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    _logger.LogWarning("⚠️ MQTT client not connected");
                    return false;
                }

                // Vérifier que l'userId correspond à l'utilisateur connecté
                if (userId != _currentUserId)
                {
                    _logger.LogWarning($"⚠️ Security: Attempt to send command for user {userId} but connected as {_currentUserId}");
                    return false;
                }

                var topic = $"devices/{userId}/{deviceId}/command";
                
                var command = new
                {
                    action = activate ? "activate" : "deactivate",
                    timestamp = DateTime.UtcNow
                };
                
                var payload = JsonSerializer.Serialize(command);

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false)
                    .Build();

                await _mqttClient.PublishAsync(message);
                
                _logger.LogInformation($"📤 Command published for user {userId} - Topic: {topic}");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to publish command for device {deviceId}");
                return false;
            }
        }

        private string ExtractUserIdFromTopic(string topic)
        {
            var parts = topic.Split('/');
            return parts.Length > 1 ? parts[1] : "unknown";
        }

        public async Task Disconnect()
        {
            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
                _logger.LogInformation($"✅ Disconnected user {_currentUserId} from MQTT broker");
                _currentUserId = string.Empty;
            }
        }

        public bool IsConnected => _mqttClient.IsConnected;
        
        public string CurrentUserId => _currentUserId;
    }

    // Classe pour parser le payload JSON des capteurs
    public class SensorPayload
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    
        [JsonPropertyName("humidity")]
        public double Humidity { get; set; }
    
        [JsonPropertyName("dust")]
        public double Dust { get; set; }  // Notez: int car 698 dans votre JSON
    
       
}

    public class DeviceStatusMessage
    {
        public bool IsActive { get; set; }
        public double? Value { get; set; }
    }

    public class ESP32RegistrationMessage
    {
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
    }
}