using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.AIServices;
using HouseVictoria.Services.Communication;
using HouseVictoria.Services.Persistence;
using HouseVictoria.Services.ProjectManagement;
using HouseVictoria.Services.SystemMonitor;
using HouseVictoria.Services.VirtualEnvironment;
using HouseVictoria.Services.FileGeneration;
using HouseVictoria.Services.Logging;
using HouseVictoria.Services.MCP;
using HouseVictoria.Services.Trading;
using HouseVictoria.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;
using System.Diagnostics;
using System.IO;

namespace HouseVictoria.App
{
    public partial class App : Application
    {
        public static ServiceProvider? ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
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
            services.AddSingleton<IAIService>(sp => 
            {
                var appConfig = sp.GetService<AppConfig>();
                return new OllamaAIService(appConfig?.OllamaEndpoint ?? "http://localhost:11434", appConfig);
            });
            // Register TTS Service
            services.AddSingleton<HouseVictoria.Core.Interfaces.ITTSService>(sp =>
            {
                try
                {
                    var appConfig = sp.GetService<AppConfig>();
                    var endpoint = appConfig?.TTSEndpoint ?? "http://localhost:5000";
                    if (string.IsNullOrWhiteSpace(endpoint))
                    {
                        endpoint = "http://localhost:5000";
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
                        return new HouseVictoria.Services.TTS.TTSService("http://localhost:5000", true, appConfig?.PiperDataDir, appConfig?.PiperDefaultModel);
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

            ServiceProvider = services.BuildServiceProvider();
        }

        private AppConfig LoadAppConfig()
        {
            var config = System.Configuration.ConfigurationManager.AppSettings;
            var appConfig = new AppConfig
            {
                OllamaEndpoint = config["OllamaEndpoint"] ?? "http://localhost:11434",
                MCPServerEndpoint = config["MCPServerEndpoint"] ?? "http://localhost:8080",
                UnrealEngineEndpoint = config["UnrealEngineEndpoint"] ?? "ws://localhost:8888",
                TTSEndpoint = config["TTSEndpoint"] ?? "http://localhost:5000",
                PiperDataDir = config["PiperDataDir"] ?? "Media/PiperVoices",
                PiperDefaultModel = config["PiperDefaultModel"] ?? "en_US-amy-medium",
                StableDiffusionEndpoint = config["StableDiffusionEndpoint"] ?? "http://localhost:8188",
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

            // Resolve pgvector connection string relative parts if needed (leave as-is if absolute)
            if (!string.IsNullOrWhiteSpace(appConfig.PgVectorConnectionString) && appConfig.PgVectorConnectionString.Contains("|DataDirectory|"))
            {
                appConfig.PgVectorConnectionString = appConfig.PgVectorConnectionString.Replace("|DataDirectory|", appDirectory);
            }

            return appConfig;
        }

        private void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("Logs/HouseVictoria-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        protected override void OnExit(ExitEventArgs e)
        {
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

            Log.CloseAndFlush();
            
            // Dispose service provider (this will dispose all IDisposable services)
            if (ServiceProvider != null)
            {
                try
                {
                    ServiceProvider.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing ServiceProvider: {ex.Message}");
                }
                ServiceProvider = null;
            }
            
            base.OnExit(e);
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
