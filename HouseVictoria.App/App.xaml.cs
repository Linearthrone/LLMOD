using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.AIServices;
using HouseVictoria.Services.Agent;
using HouseVictoria.Services.Communication;
using HouseVictoria.Services.Persistence;
using HouseVictoria.Services.ProjectManagement;
using HouseVictoria.Services.SystemMonitor;
using HouseVictoria.Services.VirtualEnvironment;
using HouseVictoria.Services.FileGeneration;
using HouseVictoria.Services.Logging;
using HouseVictoria.Services.MCP;
using HouseVictoria.Services.Trading;
using HouseVictoria.App.Services;
using HouseVictoria.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System;

namespace HouseVictoria.App
{
    public partial class App : Application
    {
        public static ServiceProvider? ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Suppress Chromium crashpad errors (harmless but noisy)
            // These errors occur when crashpad tries to register crash handlers
            Environment.SetEnvironmentVariable("CHROME_CRASH_DIR", "");
            Environment.SetEnvironmentVariable("BREAKPAD_DUMP_LOCATION", "");
            
            // Handle unobserved task exceptions globally - CRITICAL for catching background HTTP exceptions
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            
            // Handle AppDomain unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            
            // Handle dispatcher unhandled exceptions (UI thread)
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            
            // Set up exception handling BEFORE any async operations
            ConfigureExceptionHandling();
            
            try
            {
                LoggingHelper.WriteToStartupLog("Starting application...");
                
                base.OnStartup(e);
                LoggingHelper.WriteToStartupLog("base.OnStartup completed");

                try
                {
                    InitializeServices();
                    System.Diagnostics.Debug.WriteLine("Services initialized successfully");
                    LoggingHelper.WriteToStartupLog("Services initialized successfully");

                    // Apply color scheme from config
                    var appConfig = ServiceProvider?.GetService<AppConfig>();
                    if (appConfig != null && !string.IsNullOrWhiteSpace(appConfig.ColorScheme))
                    {
                        ThemeManager.ApplyTheme(appConfig.ColorScheme);
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"InitializeServices Error: {ex.Message}\nStack: {ex.StackTrace}";
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    LoggingHelper.WriteToStartupLog(errorMsg);
                    MessageBox.Show($"Service initialization failed: {ex.Message}\n\nThe application will continue but some features may not work.", "Service Init Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Continue even if service init fails
                }

                try
                {
                    ConfigureLogging();
                    System.Diagnostics.Debug.WriteLine("Logging configured successfully");
                    LoggingHelper.WriteToStartupLog("Logging configured successfully");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"ConfigureLogging Error: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    LoggingHelper.WriteToStartupLog(errorMsg);
                    // Continue even if logging fails
                }
                
                // Manually create and show MainWindow after services are initialized
                try
                {
                    LoggingHelper.WriteToStartupLog("Creating MainWindow...");
                    var mainWindow = new Screens.Windows.MainWindow();
                    mainWindow.Show();
                    mainWindow.Activate();
                    LoggingHelper.WriteToStartupLog("MainWindow created and shown");
                    System.Diagnostics.Debug.WriteLine("MainWindow created and shown successfully");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"MainWindow Creation Error: {ex.Message}\nStack: {ex.StackTrace}";
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    LoggingHelper.WriteToStartupLog(errorMsg);
                    MessageBox.Show($"Failed to create main window: {ex.Message}\n\n{ex.StackTrace}", "Window Creation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine("OnStartup completed successfully");
                LoggingHelper.WriteToStartupLog("OnStartup completed successfully");
            }
            catch (Exception ex)
            {
                var errorMsg = $"CRITICAL Startup Error: {ex.Message}\n{ex.StackTrace}";
                System.Diagnostics.Debug.WriteLine(errorMsg);
                LoggingHelper.WriteToStartupLog(errorMsg);
                MessageBox.Show($"Startup Error: {ex.Message}\n\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
        
        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Dispatcher Unhandled Exception: {e.Exception.GetType().Name}: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {e.Exception.StackTrace}");
            
            LoggingHelper.WriteExceptionToLog(e.Exception, "UnhandledExceptions.log");
            
            // Show error to user
            MessageBox.Show($"An error occurred: {e.Exception.Message}\n\nDetails have been logged.", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Mark as handled to prevent app crash, but log it
            e.Handled = true;
        }

        private void ConfigureExceptionHandling()
        {
            // Ensure all exceptions are observed, especially from HTTP operations
            // This prevents unobserved exceptions from crashing the app
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // Log unobserved task exceptions to prevent crashes
            // These often come from HttpClient's internal connection pool operations
            var exception = e.Exception.GetBaseException();
            
            // Filter out expected HTTP connection exceptions to reduce noise
            var isHttpException = exception is System.Net.Http.HttpRequestException ||
                                  exception is System.Net.Sockets.SocketException ||
                                  exception is TaskCanceledException ||
                                  exception.Message.Contains("HttpConnection") ||
                                  exception.Message.Contains("connection") ||
                                  exception.Message.Contains("timeout");
            
            if (isHttpException)
            {
                // HTTP connection failures are expected when servers are down
                // Log at debug level only
                System.Diagnostics.Debug.WriteLine($"[Expected] HTTP connection exception: {exception.GetType().Name}: {exception.Message}");
            }
            else
            {
                // Log unexpected exceptions at warning level
                System.Diagnostics.Debug.WriteLine($"Unobserved task exception: {exception.GetType().Name}: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {exception.StackTrace}");
            }
            
            // Always log to file for debugging
            LoggingHelper.WriteExceptionToLog(exception, "UnhandledExceptions.log");
            
            // ALWAYS mark as observed to prevent application crash
            // These are background exceptions that don't need to crash the app
            e.SetObserved();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Log AppDomain unhandled exceptions
            if (e.ExceptionObject is Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Unhandled exception: {exception.GetType().Name}: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {exception.StackTrace}");
                
                // Log to file
                LoggingHelper.WriteExceptionToLog(exception, "UnhandledExceptions.log");
                
                // If it's a terminating exception, show a user-friendly message
                if (e.IsTerminating)
                {
                    try
                    {
                        MessageBox.Show(
                            $"An unexpected error occurred. The application will close.\n\nError: {exception.Message}\n\nDetails have been logged.",
                            "Application Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    catch { }
                }
            }
        }

        private void InitializeServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IEventAggregator, EventAggregator>();
            services.AddSingleton<AppConfig>(sp => LoadAppConfig());
            // Register DatabasePersistenceService as a single instance, then register both interfaces to use the same instance
            services.AddSingleton<DatabasePersistenceService>(sp =>
                new DatabasePersistenceService(sp.GetService<AppConfig>()));
            services.AddSingleton<IPersistenceService>(sp => sp.GetRequiredService<DatabasePersistenceService>());
            services.AddSingleton<IMemoryService>(sp => sp.GetRequiredService<DatabasePersistenceService>());
            services.AddSingleton<IFileGenerationService>(sp =>
            {
                var appConfig = sp.GetService<AppConfig>();
                var mediaPath = appConfig?.MediaPath ?? "Media";
                return new HouseVictoria.Services.FileGeneration.FileGenerationService(mediaPath);
            });
            services.AddSingleton<OllamaAIService>(sp =>
            {
                var appConfig = sp.GetService<AppConfig>();
                return new OllamaAIService(appConfig?.OllamaEndpoint ?? "http://localhost:11434", appConfig);
            });
            services.AddSingleton<LmStudioAIService>(sp =>
            {
                var appConfig = sp.GetService<AppConfig>();
                return new LmStudioAIService(appConfig?.LmStudioEndpoint ?? "http://localhost:1234/v1");
            });
            services.AddSingleton<IAIService>(sp =>
                new FallbackAIService(
                    sp.GetRequiredService<LmStudioAIService>(),
                    sp.GetRequiredService<OllamaAIService>(),
                    sp.GetRequiredService<AppConfig>()));
            // Register TTS Service
            services.AddSingleton<HouseVictoria.Core.Interfaces.ITTSService>(sp =>
            {
                try
                {
                    var appConfig = sp.GetService<AppConfig>();
                    var endpoint = appConfig?.TTSEndpoint ?? "http://localhost:8880";
                    if (string.IsNullOrWhiteSpace(endpoint))
                    {
                        endpoint = "http://localhost:8880";
                    }
                    var piperDataDir = appConfig?.PiperDataDir;
                    var piperDefaultVoice = appConfig?.PiperDefaultModel;
                    return new HouseVictoria.Services.TTS.TTSService(endpoint, useWindowsTTSFallback: true, piperDataDir: piperDataDir, piperDefaultVoice: piperDefaultVoice);
                }
catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error creating TTS service: {ex.Message}");
                        // Return a service that will fail gracefully
                        var appConfig = sp.GetService<AppConfig>();
                        return new HouseVictoria.Services.TTS.TTSService("http://localhost:8880", true, appConfig?.PiperDataDir, appConfig?.PiperDefaultModel);
                    }
            });
            // Register CommunicationService with AI service dependency
            services.AddSingleton<ICommunicationService>(sp => 
                new SMSMMSCommunicationService(
                    sp.GetService<IAIService>(), 
                    sp.GetService<IPersistenceService>(),
                    sp.GetService<IMemoryService>(),
                    sp.GetService<IFileGenerationService>(),
                    sp.GetService<HouseVictoria.Core.Interfaces.ITTSService>()));
            services.AddSingleton<IMCPService, MCPService>();
            services.AddSingleton<IProjectManagementService, ProjectManagementService>();
            services.AddSingleton<IVirtualEnvironmentService, UnrealEnvironmentService>();
            services.AddSingleton<HouseVictoria.Services.CovasBridge.OpenAICompatibleBridge>(sp =>
                new HouseVictoria.Services.CovasBridge.OpenAICompatibleBridge(
                    sp.GetRequiredService<IAIService>(),
                    sp.GetRequiredService<IPersistenceService>(),
                    sp.GetRequiredService<AppConfig>()));
            // SystemMonitorService needs IVirtualEnvironmentService and optional COVAS bridge, so register it after
            services.AddSingleton<ISystemMonitorService>(sp =>
            {
                var mcpService = sp.GetService<IMCPService>();
                var virtualEnvService = sp.GetService<IVirtualEnvironmentService>();
                var appConfig = sp.GetService<AppConfig>();
                var covasBridge = sp.GetService<HouseVictoria.Services.CovasBridge.OpenAICompatibleBridge>();
                return new SystemMonitorService(mcpService, virtualEnvService, appConfig, covasBridge);
            });
            services.AddSingleton<ILoggingService>(sp =>
                new LoggingService(
                    sp.GetService<AppConfig>() ?? throw new InvalidOperationException("AppConfig not found"),
                    sp.GetService<IPersistenceService>() ?? throw new InvalidOperationException("IPersistenceService not found"),
                    sp.GetService<IProjectManagementService>()));
            // Register Trading Service
            services.AddSingleton<ITradingService>(sp =>
            {
                var appConfig = sp.GetService<AppConfig>();
                var service = new HouseVictoria.Services.Trading.MetaTrader4Service();
                if (!string.IsNullOrWhiteSpace(appConfig?.MT4DataPath))
                {
                    // Auto-connect if path is configured
                    _ = Task.Run(async () =>
                    {
                        await service.ConnectAsync(appConfig.MT4DataPath);
                    });
                }
                return service;
            });

            // High-level cognitive agent service (composes AI + virtual environment)
            services.AddSingleton<IAgentService, AgentService>();

            ServiceProvider = services.BuildServiceProvider();
        }

        private AppConfig LoadAppConfig()
        {
            var config = System.Configuration.ConfigurationManager.AppSettings;
            var appConfig = new AppConfig
            {
                OllamaEndpoint = config["OllamaEndpoint"] ?? "http://localhost:11434",
                LmStudioEndpoint = config["LmStudioEndpoint"] ?? "http://localhost:1234/v1",
                AnythingLLMEndpoint = config["AnythingLLMEndpoint"] ?? "http://localhost:3001",
                PrimaryLLM = config["PrimaryLLM"] ?? (bool.TryParse(config["UseLmStudioAsPrimary"], out var useLm) && useLm ? "lmstudio" : "ollama"),
                MCPServerEndpoint = config["MCPServerEndpoint"] ?? "http://localhost:8080",
                UnrealEngineEndpoint = config["UnrealEngineEndpoint"] ?? "ws://localhost:8888",
                TTSEndpoint = config["TTSEndpoint"] ?? "http://localhost:8880",
                STTEndpoint = config["STTEndpoint"],
                PiperDataDir = config["PiperDataDir"] ?? "Media/PiperVoices",
                PiperDefaultModel = config["PiperDefaultModel"] ?? "en_US-amy-medium",
                StableDiffusionEndpoint = config["StableDiffusionEndpoint"] ?? "http://localhost:7860",
                ColorScheme = config["ColorScheme"] ?? "CyanBlueDark",
                StabilityMatrixPath = config["StabilityMatrixPath"] ?? string.Empty,
                ComfyUIPortablePath = config["ComfyUIPortablePath"] ?? string.Empty,
                MT4DataPath = config["MT4DataPath"] ?? "C:\\Program Files\\MetaTrader 4",
                DataBankPath = config["DataBankPath"] ?? "Data\\Databanks",
                LogsPath = config["LogsPath"] ?? "Logs",
                MediaPath = config["MediaPath"] ?? "Media",
                EnableOverlay = bool.TryParse(config["EnableOverlay"], out var enableOverlay) && enableOverlay,
                AutoHideTrays = bool.TryParse(config["AutoHideTrays"], out var autoHideTrays) && autoHideTrays,
                EnablePgVector = bool.TryParse(config["EnablePgVector"], out var enablePg) && enablePg,
                PgVectorConnectionString = config["PgVectorConnectionString"],
                HybridLexicalWeight = double.TryParse(config["HybridLexicalWeight"], out var hybridWeight) ? hybridWeight : 0.5,
                CovasBridgeEnabled = bool.TryParse(config["CovasBridgeEnabled"], out var covasEnabled) && covasEnabled,
                CovasBridgeEndpoint = config["CovasBridgeEndpoint"] ?? "http://localhost:11435",
                CovasContactId = config["CovasContactId"] ?? string.Empty
            };

            // Resolve relative paths to absolute paths
            var appDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            appConfig.DataBankPath = System.IO.Path.IsPathRooted(appConfig.DataBankPath) 
                ? appConfig.DataBankPath 
                : System.IO.Path.Combine(appDirectory, appConfig.DataBankPath);
            appConfig.LogsPath = System.IO.Path.IsPathRooted(appConfig.LogsPath) 
                ? appConfig.LogsPath 
                : System.IO.Path.Combine(appDirectory, appConfig.LogsPath);
            appConfig.MediaPath = System.IO.Path.IsPathRooted(appConfig.MediaPath) 
                ? appConfig.MediaPath 
                : System.IO.Path.Combine(appDirectory, appConfig.MediaPath);
            // Piper voices: resolve relative path; prefer Media\PiperVoices next to app or under a parent that contains Media
            var piperDataDirRelative = appConfig.PiperDataDir;
            if (!System.IO.Path.IsPathRooted(appConfig.PiperDataDir))
            {
                var combined = System.IO.Path.Combine(appDirectory, appConfig.PiperDataDir);
                if (System.IO.Directory.Exists(combined))
                    appConfig.PiperDataDir = System.IO.Path.GetFullPath(combined);
                else
                {
                    var dir = appDirectory;
                    while (!string.IsNullOrEmpty(dir))
                    {
                        var mediaDir = System.IO.Path.Combine(dir, "Media");
                        if (System.IO.Directory.Exists(mediaDir))
                        {
                            appConfig.PiperDataDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, appConfig.PiperDataDir));
                            break;
                        }
                        var parent = System.IO.Path.GetDirectoryName(dir);
                        if (parent == dir) break;
                        dir = parent;
                    }
                    if (!System.IO.Path.IsPathRooted(appConfig.PiperDataDir))
                        appConfig.PiperDataDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(appDirectory, appConfig.PiperDataDir));
                }
                // If resolved path still doesn't exist, try current directory (e.g. when run from repo root)
                if (!System.IO.Directory.Exists(appConfig.PiperDataDir) && !string.IsNullOrEmpty(piperDataDirRelative))
                {
                    var currentDirPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Environment.CurrentDirectory, piperDataDirRelative));
                    if (System.IO.Directory.Exists(currentDirPath))
                        appConfig.PiperDataDir = currentDirPath;
                }
            }

            // Resolve pgvector connection string relative parts if needed (leave as-is if absolute)
            if (!string.IsNullOrWhiteSpace(appConfig.PgVectorConnectionString) && appConfig.PgVectorConnectionString.Contains("|DataDirectory|"))
            {
                appConfig.PgVectorConnectionString = appConfig.PgVectorConnectionString.Replace("|DataDirectory|", appDirectory);
            }

            return appConfig;
        }

        private void ConfigureLogging()
        {
            // Keep Serilog's file output aligned with AppConfig.LogsPath so GLD can discover it reliably
            // regardless of current working directory.
            var appConfig = ServiceProvider?.GetService<AppConfig>();
            var logsDir = appConfig?.LogsPath;
            if (string.IsNullOrWhiteSpace(logsDir))
            {
                logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            }

            Directory.CreateDirectory(logsDir);
            var logFilePath = Path.Combine(logsDir, "HouseVictoria-.log");

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                LoggingHelper.WriteToStartupLog("Application shutting down...");
                System.Diagnostics.Debug.WriteLine("Application shutting down - saving data and stopping services...");
            }
            catch { }

            // Unsubscribe from event handlers
            try
            {
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            }
            catch { }
            
            try
            {
                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            }
            catch { }
            
            try
            {
                this.DispatcherUnhandledException -= OnDispatcherUnhandledException;
            }
            catch { }

            // Save unsaved data and shut down services gracefully
            if (ServiceProvider != null)
            {
                try
                {
                    // Save any unsaved data first
                    SaveUnsavedData();

                    // Stop async services gracefully
                    StopServicesAsync().GetAwaiter().GetResult();

                    // Dispose service provider (this will dispose all IDisposable services)
                    ServiceProvider.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex.Message}");
                    LoggingHelper.WriteExceptionToLog(ex, "ShutdownErrors.log");
                }
                ServiceProvider = null;
            }

            Log.CloseAndFlush();
            
            base.OnExit(e);
        }

        private void SaveUnsavedData()
        {
            try
            {
                // Save logging service read status
                var loggingService = ServiceProvider?.GetService<ILoggingService>();
                if (loggingService != null)
                {
                    // Use reflection to call SaveReadStatusAsync since it's private
                    // This ensures any pending read status changes are persisted
                    var saveMethod = loggingService.GetType().GetMethod("SaveReadStatusAsync", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (saveMethod != null)
                    {
                        try
                        {
                            var task = (Task?)saveMethod.Invoke(loggingService, null);
                            if (task != null)
                            {
                                task.GetAwaiter().GetResult();
                                System.Diagnostics.Debug.WriteLine("Logging service read status saved");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error saving logging read status: {ex.Message}");
                        }
                    }
                }

                // Save any pending database transactions
                var persistenceService = ServiceProvider?.GetService<IPersistenceService>();
                if (persistenceService != null)
                {
                    // SQLite connections are auto-committed, but we can ensure any pending operations complete
                    // by accessing the service (which may trigger any lazy initialization/flush)
                    System.Diagnostics.Debug.WriteLine("Persistence service checked for pending saves");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving unsaved data: {ex.Message}");
            }
        }

        private async Task StopServicesAsync()
        {
            try
            {
                // Stop SystemMonitorService servers (includes LocalTtsHttpHost and COVAS bridge)
                var systemMonitorService = ServiceProvider?.GetService<ISystemMonitorService>();
                if (systemMonitorService != null)
                {
                    try
                    {
                        await systemMonitorService.ShutdownAllServersAsync().ConfigureAwait(false);
                        System.Diagnostics.Debug.WriteLine("SystemMonitorService servers stopped (including TTS host and COVAS bridge)");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error stopping SystemMonitorService servers: {ex.Message}");
                    }
                }

                // Disconnect virtual environment service if connected
                var virtualEnvService = ServiceProvider?.GetService<IVirtualEnvironmentService>();
                if (virtualEnvService != null)
                {
                    try
                    {
                        var envStatus = await virtualEnvService.GetStatusAsync().ConfigureAwait(false);
                        if (envStatus.IsConnected)
                        {
                            await virtualEnvService.DisconnectAsync().ConfigureAwait(false);
                            System.Diagnostics.Debug.WriteLine("Virtual environment service disconnected");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disconnecting virtual environment service: {ex.Message}");
                    }
                }

                // Disconnect trading service if connected
                var tradingService = ServiceProvider?.GetService<ITradingService>();
                if (tradingService != null)
                {
                    try
                    {
                        var status = await tradingService.GetStatusAsync().ConfigureAwait(false);
                        if (status.IsConnected)
                        {
                            await tradingService.DisconnectAsync().ConfigureAwait(false);
                            System.Diagnostics.Debug.WriteLine("Trading service disconnected");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disconnecting trading service: {ex.Message}");
                    }
                }

                // Give services a moment to finish cleanup
                await Task.Delay(500).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping services: {ex.Message}");
            }
        }

        public static T GetService<T>() where T : class
        {
            if (ServiceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider has not been initialized");
            }
            return ServiceProvider.GetService<T>() ?? throw new InvalidOperationException($"Service of type {typeof(T).Name} not found");
        }
    }
}
