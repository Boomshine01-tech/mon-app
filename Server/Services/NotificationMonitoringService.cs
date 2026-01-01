// SmartNest.Server/Services/NotificationMonitoringService.cs
// VERSION MISE À JOUR avec dispatch Email/SMS
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SmartNest.Server.Data;
using SmartNest.Server.Models.postgres;


namespace SmartNest.Server.Services
{
    /// <summary>
    /// Service en arrière-plan qui surveille en continu les changements
    /// et envoie des notifications via Email/SMS/Push
    /// </summary>
    public class NotificationMonitoringService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NotificationMonitoringService> _logger;
        
        // Dictionnaires pour suivre les états précédents
        private Dictionary<string, bool> _previousDeviceStates = new();
        private Dictionary<string, double> _lastSensorValues = new();
        private HashSet<string> _notifiedSickChicks = new();

        public NotificationMonitoringService(
            IServiceProvider serviceProvider,
            ILogger<NotificationMonitoringService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service de monitoring des notifications démarré avec dispatch Email/SMS");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var dispatcherService = scope.ServiceProvider.GetRequiredService<INotificationDispatcherService>();

                    // 1. Vérifier les changements d'état des devices
                    await CheckDeviceStateChanges(context, notificationService, dispatcherService);

                    // 2. Vérifier les seuils des capteurs
                    await CheckSensorThresholds(context, notificationService, dispatcherService);

                    // 3. Vérifier les poussins malades
                    await CheckSickChicks(context, notificationService, dispatcherService);

                    // Attendre 30 secondes avant la prochaine vérification
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur dans le service de monitoring des notifications");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }

            _logger.LogInformation("Service de monitoring des notifications arrêté");
        }

        private async Task CheckDeviceStateChanges(
            ApplicationDbContext context, 
            INotificationService notificationService,
            INotificationDispatcherService dispatcherService)
        {
            try
            {
                var devices = await context.Devices
                    .Where(d => d.IsActive)
                    .ToListAsync();

                foreach (var device in devices)
                {
                    string deviceKey = device.DeviceId;
                    bool currentState = device.IsActive;

                    if (_previousDeviceStates.TryGetValue(deviceKey, out bool previousState))
                    {
                        if (previousState != currentState)
                        {
                            await CreateAndDispatchDeviceNotification(
                                context, 
                                notificationService, 
                                dispatcherService,
                                device, 
                                currentState);
                        }
                    }

                    _previousDeviceStates[deviceKey] = currentState;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification des états des devices");
            }
        }

        private async Task CreateAndDispatchDeviceNotification(
            ApplicationDbContext context,
            INotificationService notificationService,
            INotificationDispatcherService dispatcherService,
            device device,
            bool newState)
        {
            try
            {
                // Récupérer l'utilisateur propriétaire du device
                if (string.IsNullOrEmpty(device.UserId))
                {
                    _logger.LogWarning("Device {DeviceId} n'a pas de UserId assigné", device.DeviceId);
                    return;
                }

                string userId = device.UserId;
                string stateText = newState ? "activé" : "désactivé";
                string severity = newState ? "Success" : "Warning";

                var request = new CreateNotificationRequest
                {
                    UserId = userId,
                    Title = $"Changement d'état: {device.DeviceName}",
                    Message = $"Le dispositif {device.DeviceName} a été {stateText}",
                    Category = device.DeviceType,
                    Severity = severity,
                    DeviceId = device.DeviceId,
                    DeviceName = device.DeviceName,
                    ActionTaken = $"État changé: {stateText}"
                };

                // Créer la notification en base de données
                await notificationService.CreateNotificationAsync(request);
                
                // Dispatcher via Email/SMS selon préférences
                await dispatcherService.DispatchNotificationAsync(request);
                
                _logger.LogInformation(
                    "Notification device dispatchée: {DeviceId} - {State}", 
                    device.DeviceId, 
                    stateText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création/dispatch de notification device");
            }
        }

        private async Task CheckSensorThresholds(
            ApplicationDbContext context,
            INotificationService notificationService,
            INotificationDispatcherService dispatcherService)
        {
            try
            {
                var recentSensorData = await context.SensorData
                    .Where(s => s.timestamp >= DateTime.UtcNow.AddMinutes(-5))
                    .OrderByDescending(s => s.timestamp)
                    .ToListAsync();

                var latestByDevice = recentSensorData
                    .GroupBy(s => s.id)
                    .Select(g => g.First())
                    .ToList();

                foreach (var sensorData in latestByDevice)
                {
                  

                   

                    // Récupérer l'utilisateur propriétaire du device
                    if (string.IsNullOrEmpty(sensorData.userid))
                    {
                        _logger.LogWarning("Device {DeviceId} n'a pas de UserId assigné", sensorData.id);
                        continue;
                    }

                    string userId = sensorData.userid;

                    var settings = await context.NotificationSettings
                        .FirstOrDefaultAsync(ns => ns.UserId == userId);

                    if (settings == null || !settings.NotificationsEnabled)
                        continue;

                    // Vérifier tous les seuils et dispatcher
                    await CheckAndDispatchThreshold(
                        sensorData, userId, settings, 
                        notificationService, dispatcherService);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification des seuils");
            }
        }

        private async Task CheckAndDispatchThreshold(
            Sensordatum sensorData,
            string userId,
            NotificationSettings settings,
            INotificationService notificationService,
            INotificationDispatcherService dispatcherService)
        {
            // Température

                double temp = sensorData.temperature;
                string sensorKey = $"{sensorData.id}_temperature";

                if (temp > settings.TemperatureThreshold)
                {
                    if (!_lastSensorValues.ContainsKey(sensorKey) || 
                        Math.Abs(_lastSensorValues[sensorKey] - temp) > 2.0)
                    {
                        var request = new CreateNotificationRequest
                        {
                            UserId = userId,
                            Title = "⚠️ Température élevée détectée",
                            Message = $"La température ({temp:F1}°C) dépasse le seuil de {settings.TemperatureThreshold}°C",
                            Category = "Température",
                            Severity = "Critical",
                            TriggerValue = temp,
                            ThresholdValue = settings.TemperatureThreshold
                        };

                        await notificationService.CreateNotificationAsync(request);
                        await dispatcherService.DispatchNotificationAsync(request);
                        
                        _lastSensorValues[sensorKey] = temp;
                    }
                }
                else
                {
                    _lastSensorValues.Remove(sensorKey);
                }
            

            // Humidité
            
                double humidity = sensorData.humidity;
                string sensorKey1 = $"{sensorData.id}_humidity";

                if (humidity < settings.HumidityThreshold)
                {
                    if (!_lastSensorValues.ContainsKey(sensorKey1) || 
                        Math.Abs(_lastSensorValues[sensorKey1] - humidity) > 5.0)
                    {
                        var request = new CreateNotificationRequest
                        {
                            UserId = userId,
                            Title = "⚠️ Humidité faible",
                            Message = $"L'humidité ({humidity:F1}%) est inférieure au seuil de {settings.HumidityThreshold}%",
                            Category = "Humidité",
                            Severity = "Warning",
                            TriggerValue = humidity,
                            ThresholdValue = settings.HumidityThreshold
                        };

                        await notificationService.CreateNotificationAsync(request);
                        await dispatcherService.DispatchNotificationAsync(request);
                        
                        _lastSensorValues[sensorKey1] = humidity;
                    }
                }
                else
                {
                    _lastSensorValues.Remove(sensorKey);
                }
            

            // Niveau d'eau (CRITIQUE - envoi SMS)
            
            //    double waterLevel = sensorData.WaterLevel;
              //  string sensorKey2 = $"{sensorData.id}_water";

                //if (waterLevel < settings.WaterLevelThreshold)
                //{
                  //  if (!_lastSensorValues.ContainsKey(sensorKey2) || 
                    //    Math.Abs(_lastSensorValues[sensorKey2] - waterLevel) > 5.0)
                    //{
                      //  var request = new CreateNotificationRequest
                        //{
                          //  UserId = userId,
                            //Title = "🚨 Niveau d'eau critique",
                        //    Message = $"Le niveau d'eau ({waterLevel:F1}%) est dangereusement bas",
                          //  Category = "Distributeur eau",
                            //Severity = "Critical", // SMS sera envoyé
                            //TriggerValue = waterLevel,
                        //    ThresholdValue = settings.WaterLevelThreshold,
                          //  ActionTaken = "Remplissez immédiatement le distributeur d'eau"
                        //};

                    //    await notificationService.CreateNotificationAsync(request);
                      //  await dispatcherService.DispatchNotificationAsync(request);
                        
                        //_lastSensorValues[sensorKey2] = waterLevel;
                    //}
                //}
                //else
                //{
                  //  _lastSensorValues.Remove(sensorKey2);
                //}
            
        }

        private async Task CheckSickChicks(
            ApplicationDbContext context,
            INotificationService notificationService,
            INotificationDispatcherService dispatcherService)
        {
            try
            {
                var sickChicks = await context.Chicks
                    .Where(c => c.healthstate == "Malade" )
                    .ToListAsync();

                foreach (var chick in sickChicks)
                {
                    string chickKey = chick.ChickId;

                    if (!_notifiedSickChicks.Contains(chickKey))
                    {
                        // Récupérer l'utilisateur propriétaire du poussin
                        if (string.IsNullOrEmpty(chick.UserId))
                        {
                            _logger.LogWarning("Chick {ChickId} n'a pas de UserId assigné", chick.ChickId);
                            continue;
                        }

                        string userId = chick.UserId;

                        var request = new CreateNotificationRequest
                        {
                            UserId = userId,
                            Title = "🚨 Poussin malade détecté",
                            Message = $"Le poussin '{chick.ChickId}' a été détecté comme malade",
                            Category = "Poussins",
                            Severity = "Critical", // SMS sera envoyé
                            ActionTaken = "Isolez le poussin et contactez un vétérinaire"
                        };

                        await notificationService.CreateNotificationAsync(request);
                        await dispatcherService.DispatchNotificationAsync(request);
                        
                        _notifiedSickChicks.Add(chickKey);
                    }
                }

                var currentSickChickIds = sickChicks.Select(c => c.ChickId).ToHashSet();
                _notifiedSickChicks.RemoveWhere(id => !currentSickChickIds.Contains(id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification des poussins malades");
            }
        }
    }
}