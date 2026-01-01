using System.Diagnostics;

namespace SmartNest.Server.Services
{
    public interface IYoloProcessManager
    {
        Task StartYoloServerAsync();
        Task StopYoloServerAsync();
        bool IsYoloServerRunning();
    }

    public class YoloProcessManager : IYoloProcessManager, IDisposable
    {
        private Process? _yoloProcess;
        private readonly ILogger<YoloProcessManager> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _pythonPath;
        private readonly string _scriptPath;
        private readonly int _yoloPort;

        public YoloProcessManager(
            ILogger<YoloProcessManager> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            // Configuration depuis appsettings.json
            _pythonPath = configuration["YoloApi:PythonPath"] ?? "C:/Users/bmd tech/anaconda3/envs/poussin/python.exe";
            var scriptPathFromConfig = configuration["YoloApi:ScriptPath"] ?? "Scripts/yolo_server.py";
            
            // Convertir en chemin absolu pour éviter les problèmes
            _scriptPath = Path.IsPathRooted(scriptPathFromConfig) 
                ? scriptPathFromConfig 
                : Path.GetFullPath(scriptPathFromConfig);
            
            _yoloPort = int.Parse(configuration["YoloApi:Port"] ?? "5000");
        }

        public async Task StartYoloServerAsync()
        {
            if (IsYoloServerRunning())
            {
                _logger.LogInformation("✅ YOLO server is already running");
                return;
            }

            try
            {
                _logger.LogInformation("🚀 Starting YOLO server...");
                _logger.LogInformation($"Python: {_pythonPath}");
                _logger.LogInformation($"Script: {_scriptPath}");

                // Vérifier que le script existe
                if (!File.Exists(_scriptPath))
                {
                    _logger.LogError($"❌ YOLO script not found at: {_scriptPath}");
                    throw new FileNotFoundException($"YOLO script not found: {_scriptPath}");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = $"\"{_scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                    // Ne pas définir WorkingDirectory, utilise le répertoire courant de l'application
                };

                _yoloProcess = new Process { StartInfo = startInfo };

                // Capturer les logs Python
                _yoloProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogInformation($"[YOLO] {e.Data}");
                    }
                };

                _yoloProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogWarning($"[YOLO Error] {e.Data}");
                    }
                };

                _yoloProcess.Start();
                _yoloProcess.BeginOutputReadLine();
                _yoloProcess.BeginErrorReadLine();

                // Attendre que le serveur soit prêt (max 30 secondes)
                var startTime = DateTime.UtcNow;
                var timeout = TimeSpan.FromSeconds(30);
                var httpClient = new HttpClient();

                while (DateTime.UtcNow - startTime < timeout)
                {
                    try
                    {
                        var response = await httpClient.GetAsync($"http://localhost:{_yoloPort}/health");
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation($"✅ YOLO server started successfully on port {_yoloPort}");
                            return;
                        }
                    }
                    catch
                    {
                        // Serveur pas encore prêt
                    }

                    await Task.Delay(1000);
                }

                _logger.LogWarning("⚠️ YOLO server started but health check failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to start YOLO server");
                throw;
            }
        }

        public async Task StopYoloServerAsync()
        {
            if (_yoloProcess != null && !_yoloProcess.HasExited)
            {
                try
                {
                    _logger.LogInformation("🛑 Stopping YOLO server...");
                    
                    _yoloProcess.Kill(entireProcessTree: true);
                    await _yoloProcess.WaitForExitAsync();
                    
                    _logger.LogInformation("✅ YOLO server stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping YOLO server");
                }
                finally
                {
                    _yoloProcess?.Dispose();
                    _yoloProcess = null;
                }
            }
        }

        public bool IsYoloServerRunning()
        {
            return _yoloProcess != null && !_yoloProcess.HasExited;
        }

        public void Dispose()
        {
            StopYoloServerAsync().GetAwaiter().GetResult();
        }
    }

    // Hosted Service pour démarrer automatiquement au lancement de l'application
    public class YoloServerHostedService : IHostedService
    {
        private readonly IYoloProcessManager _yoloManager;
        private readonly ILogger<YoloServerHostedService> _logger;
        private readonly IConfiguration _configuration;

        public YoloServerHostedService(
            IYoloProcessManager yoloManager,
            ILogger<YoloServerHostedService> logger,
            IConfiguration configuration)
        {
            _yoloManager = yoloManager;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var autoStart = _configuration.GetValue<bool>("YoloApi:AutoStart", true);
            
            if (!autoStart)
            {
                _logger.LogInformation("⏸️ YOLO auto-start is disabled");
                return;
            }

            try
            {
                _logger.LogInformation("🚀 Auto-starting YOLO server...");
                await _yoloManager.StartYoloServerAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to auto-start YOLO server");
                _logger.LogWarning("⚠️ Application will continue without YOLO");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 Stopping YOLO server...");
            await _yoloManager.StopYoloServerAsync();
        }
    }
}