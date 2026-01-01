using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace SmartNest.Client.Services
{
    public interface IDeviceService
    {
        Task<List<DeviceModel>> GetDevicesAsync();
        Task<bool> ToggleDeviceAsync(string deviceId, bool activate);
        Task ConnectToRealtimeUpdatesAsync(Action onDeviceUpdated);
        Task DisconnectFromRealtimeUpdatesAsync();
        event Action<DeviceModel> OnDeviceStatusChanged;
    }

    public class DeviceService : IDeviceService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DeviceService> _logger;
        private HubConnection? _hubConnection;
        private List<DeviceModel> _cachedDevices = new();

        public event Action<DeviceModel>? OnDeviceStatusChanged;

        public DeviceService(HttpClient httpClient, ILogger<DeviceService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<DeviceModel>> GetDevicesAsync()
        {
            try
            {
                _logger.LogInformation("📡 Fetching devices from API...");
                
                var devices = await _httpClient.GetFromJsonAsync<List<DeviceResponse>>("api/devices");
                
                if (devices != null)
                {
                    _cachedDevices = devices.Select(d => new DeviceModel
                    {
                        DeviceId = d.DeviceId,
                        DeviceName = d.DeviceName,
                        DeviceType = d.DeviceType,
                        DeviceIcon = GetIconForDeviceType(d.DeviceType),
                        IsActive = d.IsActive,
                        LastUpdated = d.LastUpdated,
                        StatusMessage = d.StatusMessage,
                        FormattedTimeSinceChange = FormatTimeSince(d.LastUpdated)
                    }).ToList();

                    _logger.LogInformation($"✅ Loaded {_cachedDevices.Count} devices");
                    return _cachedDevices;
                }

                _logger.LogWarning("⚠️ No devices returned from API");
                return new List<DeviceModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching devices");
                return _cachedDevices; // Retourner le cache en cas d'erreur
            }
        }

        public async Task<bool> ToggleDeviceAsync(string deviceId, bool activate)
        {
            try
            {
                _logger.LogInformation($"🔄 Toggling device {deviceId} to {activate}");

                var response = await _httpClient.PostAsJsonAsync($"api/devices/{deviceId}/toggle", activate);

                if (response.IsSuccessStatusCode)
                {
                    // Mise à jour optimiste du cache local
                    var cachedDevice = _cachedDevices.FirstOrDefault(d => d.DeviceId == deviceId);
                    if (cachedDevice != null)
                    {
                        cachedDevice.IsActive = activate;
                        cachedDevice.LastUpdated = DateTime.Now;
                        cachedDevice.StatusMessage = activate ? "Appareil activé" : "Appareil désactivé";
                        cachedDevice.FormattedTimeSinceChange = "À l'instant";
                    }

                    _logger.LogInformation($"✅ Device {deviceId} toggled successfully");
                    return true;
                }

                _logger.LogWarning($"⚠️ Failed to toggle device {deviceId}: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error toggling device {deviceId}");
                return false;
            }
        }

        public async Task ConnectToRealtimeUpdatesAsync(Action onDeviceUpdated)
        {
            try
            {
                _logger.LogInformation("🔌 Connecting to SignalR hub...");

                // Construire l'URL du hub SignalR
                var hubUrl = new Uri(_httpClient.BaseAddress!, "hubs/realtime").ToString();
                
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl)
                    .WithAutomaticReconnect()
                    .ConfigureLogging(logging =>
                    {
                        logging.SetMinimumLevel(LogLevel.Information);
                    })
                    .Build();

                // Écouter les changements de statut d'appareils
                _hubConnection.On<DeviceStatusChangedEvent>("DeviceStatusChanged", (data) =>
                {
                    try
                    {
                        _logger.LogInformation($"📩 SignalR: Device status changed - {data.DeviceId}");

                        // Mettre à jour le cache local
                        var device = _cachedDevices.FirstOrDefault(d => d.DeviceId == data.DeviceId);
                        if (device != null)
                        {
                            device.IsActive = data.IsActive;
                            device.LastUpdated = data.LastUpdated;
                            device.StatusMessage = data.StatusMessage;
                            device.FormattedTimeSinceChange = "À l'instant";

                            // Notifier les composants abonnés
                            OnDeviceStatusChanged?.Invoke(device);
                        }
                        else
                        {
                            // Ajouter le nouvel appareil au cache
                            var newDevice = new DeviceModel
                            {
                                DeviceId = data.DeviceId,
                                DeviceName = data.DeviceName,
                                DeviceType = data.DeviceType,
                                DeviceIcon = GetIconForDeviceType(data.DeviceType),
                                IsActive = data.IsActive,
                                LastUpdated = data.LastUpdated,
                                StatusMessage = data.StatusMessage,
                                FormattedTimeSinceChange = "À l'instant"
                            };
                            _cachedDevices.Add(newDevice);
                            OnDeviceStatusChanged?.Invoke(newDevice);
                        }

                        // Notifier le composant parent
                        onDeviceUpdated?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Error processing device status update");
                    }
                });

                // Gérer les reconnexions
                _hubConnection.Reconnecting += error =>
                {
                    _logger.LogWarning("⚠️ SignalR reconnecting...");
                    return Task.CompletedTask;
                };

                _hubConnection.Reconnected += connectionId =>
                {
                    _logger.LogInformation($"✅ SignalR reconnected: {connectionId}");
                    return Task.CompletedTask;
                };

                _hubConnection.Closed += error =>
                {
                    _logger.LogError(error, "❌ SignalR connection closed");
                    return Task.CompletedTask;
                };

                // Démarrer la connexion
                await _hubConnection.StartAsync();
                _logger.LogInformation($"✅ Connected to SignalR hub (State: {_hubConnection.State})");

                // Joindre le groupe de l'utilisateur
                await _hubConnection.InvokeAsync("JoinUserGroup", "123"); // TODO: userId dynamique
                _logger.LogInformation("✅ Joined user group");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to connect to SignalR");
            }
        }

        public async Task DisconnectFromRealtimeUpdatesAsync()
        {
            if (_hubConnection != null)
            {
                try
                {
                    await _hubConnection.StopAsync();
                    await _hubConnection.DisposeAsync();
                    _logger.LogInformation("✅ Disconnected from SignalR hub");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error disconnecting from SignalR");
                }
            }
        }

        private string GetIconForDeviceType(string deviceType)
        {
            return deviceType switch
            {
                "Fan" => "fas fa-fan",
                "HeatLamp" => "fas fa-lightbulb",
                "Feeder" => "fas fa-utensils",
                "WaterDispenser" => "fas fa-tint",
                _ => "fas fa-plug"
            };
        }

        private string FormatTimeSince(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;
            if (timeSpan.TotalSeconds < 60) return "À l'instant";
            if (timeSpan.TotalMinutes < 60) return $"Il y a {(int)timeSpan.TotalMinutes} min";
            if (timeSpan.TotalHours < 24) return $"Il y a {(int)timeSpan.TotalHours}h";
            return $"Il y a {(int)timeSpan.TotalDays}j";
        }
    }

    // Modèles de données
    public class DeviceModel
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string DeviceIcon { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime LastUpdated { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public string FormattedTimeSinceChange { get; set; } = string.Empty;
    }

    public class DeviceResponse
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime LastUpdated { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }

    public class DeviceStatusChangedEvent
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime LastUpdated { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }
}