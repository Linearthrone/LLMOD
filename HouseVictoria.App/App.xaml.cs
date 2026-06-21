using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.AIServices;
using HouseVictoria.Services.Agent;
using HouseVictoria.Services.Communication;
using HouseVictoria.Services.Autonomy;
using HouseVictoria.Services.Persistence;
using HouseVictoria.Services.SystemMonitor;
using HouseVictoria.Services.VirtualEnvironment;
using HouseVictoria.Services.Logging;
using HouseVictoria.Services.MCP;
using HouseVictoria.Services.RemoteCompanion;
using HouseVictoria.App.RemoteCompanion;
using HouseVictoria.App.Services;
using HouseVictoria.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;
using System.IO;

namespace HouseVictoria.App
{
    public partial class App : Application
    {
        public static ServiceProvider? ServiceProvider { get; private set; }

        private RemoteCompanionWebHost? _remoteCompanionHost;

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

                    // Apply color scheme from config (migrate legacy default to Obsidian Field)
                    var appConfig = ServiceProvider?.GetService<AppConfig>();
                    if (appConfig != null)
                    {
                        if (string.Equals(appConfig.ColorScheme, "CyanBlueDark", StringComparison.OrdinalIgnoreCase))
                        {
                            appConfig.ColorScheme = "ObsidianFieldDark";
                        }

                        if (!string.IsNullOrWhiteSpace(appConfig.ColorScheme))
                        {
                            ThemeManager.ApplyTheme(appConfig.ColorScheme);
                        }
                        else
                        {
                            ThemeManager.ApplyTheme("ObsidianFieldDark");
                        }
                    }

                    // Load persisted primary/secondary persona selections and migrate the legacy
                    // IsPrimaryAI flag before any background service resolves the active persona.
                    try
                    {
                        ServiceProvider?.GetService<IPersonaContext>()?.InitializeAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception personaEx)
                    {
                        LoggingHelper.WriteToStartupLog($"PersonaContext init error: {personaEx.Message}");
                    }

                    _ = SyncPersonaMcpEndpointsAsync();
                    _ = SyncPersonaKnowledgeSharingAsync();
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

                // Allow running only the remote companion API host (no UI window).
                // Safety: require BOTH env flag and explicit CLI arg to avoid accidental
                // headless launches when the env var leaks into normal user sessions.
                var remoteOnlyEnv = string.Equals(
                    Environment.GetEnvironmentVariable("HV_REMOTE_COMPANION_ONLY"),
                    "1",
                    StringComparison.Ordinal);
                var remoteOnlyArg = e.Args.Any(a =>
                    string.Equals(a, "--remote-only", StringComparison.OrdinalIgnoreCase));
                var remoteOnly = remoteOnlyEnv && remoteOnlyArg;
                if (remoteOnly)
                {
                    LoggingHelper.WriteToStartupLog("Remote-only mode requested (env + --remote-only); starting remote companion host without UI.");
                    ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    StartRemoteCompanionHost();
                    LoggingHelper.WriteToStartupLog("Remote-only mode initialized.");
                    return;
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

                    StartRemoteCompanionHost();
                    StartAarService();
                    StartAutonomyLoop();
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

        private void StartAarService()
        {
            try
            {
                // Resolve the singleton so it subscribes to project-completion events and can
                // generate After Action Reports even before the AAR tray is opened.
                _ = ServiceProvider?.GetService<IAarService>();
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteToStartupLog($"AAR service setup error: {ex.Message}");
            }
        }

        private void StartAutonomyLoop()
        {
            try
            {
                var autonomy = ServiceProvider?.GetService<IAutonomyService>();
                var appConfig = ServiceProvider?.GetService<AppConfig>();
                if (autonomy == null || appConfig == null || !appConfig.EnableAutonomy || appConfig.AutonomyLevel == AutonomyLevel.Off)
                    return;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await autonomy.StartAsync().ConfigureAwait(false);
                        LoggingHelper.WriteToStartupLog("Autonomy loop started.");
                    }
                    catch (Exception ex)
                    {
                        LoggingHelper.WriteToStartupLog($"Autonomy loop failed to start: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteToStartupLog($"Autonomy setup error: {ex.Message}");
            }
        }

        private void StartRemoteCompanionHost()
        {
            try
            {
                _remoteCompanionHost = new RemoteCompanionWebHost();
                var host = _remoteCompanionHost;
                var sp = ServiceProvider;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (sp != null)
                            await host.StartIfEnabledAsync(sp).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LoggingHelper.WriteToStartupLog($"Remote companion host failed to start: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteToStartupLog($"Remote companion host setup error: {ex.Message}");
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
            // Single source of truth for primary (always-active) vs secondary (last activated in chat) persona.
            services.AddSingleton<IPersonaContext>(sp =>
                new HouseVictoria.Services.Persona.PersonaContextService(
                    sp.GetRequiredService<IPersistenceService>(),
                    sp.GetRequiredService<AppConfig>(),
                    sp.GetService<IEventAggregator>()));
            services.AddSingleton<IPersonaBackupService>(sp =>
                new HouseVictoria.Services.Persona.PersonaBackupService(
                    sp.GetRequiredService<DatabasePersistenceService>(),
                    sp.GetRequiredService<IMemoryService>(),
                    sp.GetRequiredService<AppConfig>()));
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
            services.AddSingleton<IHermesGatewayService>(sp =>
            {
                var appConfig = sp.GetRequiredService<AppConfig>();
                var root = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
                while (!string.IsNullOrEmpty(root) && !Directory.Exists(Path.Combine(root, "MCPServer")))
                {
                    var parent = Directory.GetParent(root)?.FullName;
                    if (parent == null || parent == root) break;
                    root = parent;
                }
                return new HouseVictoria.Services.Hermes.HermesGatewayService(appConfig, root);
            });
            services.AddSingleton<HermesAIService>(sp =>
                new HermesAIService(
                    sp.GetRequiredService<AppConfig>(),
                    sp.GetService<IHermesGatewayService>()));
            services.AddSingleton<IAIService>(sp =>
                new FallbackAIService(
                    sp.GetRequiredService<LmStudioAIService>(),
                    sp.GetRequiredService<OllamaAIService>(),
                    sp.GetRequiredService<HermesAIService>(),
                    sp.GetRequiredService<AppConfig>()));
            // Register CommunicationService with AI service dependency
            services.AddSingleton<IVoiceCallEngineService, HouseVictoria.Services.Voice.VoiceCallEngineService>();
            services.AddSingleton<ICommunicationService>(sp =>
                new SMSMMSCommunicationService(
                    sp.GetService<IAIService>(),
                    sp.GetService<IPersistenceService>(),
                    sp.GetService<IMemoryService>(),
                    sp.GetService<IFileGenerationService>(),
                    sp.GetService<IJournalService>(),
                    sp.GetService<IVoiceCallEngineService>(),
                    sp.GetService<AppConfig>()));
            services.AddSingleton<IMCPService, MCPService>();
            services.AddSingleton<IProjectManagementService, HouseVictoria.Services.ProjectManagement.PersistentProjectManagementService>();
            services.AddSingleton<IVirtualEnvironmentService, UnrealEnvironmentService>();
            services.AddSingleton<HouseVictoria.Services.CovasBridge.OpenAICompatibleBridge>(sp =>
                new HouseVictoria.Services.CovasBridge.OpenAICompatibleBridge(
                    sp.GetRequiredService<IAIService>(),
                    sp.GetRequiredService<IPersistenceService>(),
                    sp.GetRequiredService<AppConfig>(),
                    sp.GetService<IPersonaContext>()));
            // SystemMonitorService needs IVirtualEnvironmentService and optional COVAS bridge, so register it after
            services.AddSingleton<ISystemMonitorService>(sp =>
            {
                var mcpService = sp.GetService<IMCPService>();
                var virtualEnvService = sp.GetService<IVirtualEnvironmentService>();
                var appConfig = sp.GetService<AppConfig>();
                var covasBridge = sp.GetService<HouseVictoria.Services.CovasBridge.OpenAICompatibleBridge>();
                return new SystemMonitorService(mcpService, virtualEnvService, appConfig, covasBridge, sp.GetService<IHermesGatewayService>());
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
                    _ = Task.Run(async () => await service.ConnectAsync(appConfig.MT4DataPath));
                }
                return service;
            });
            services.AddSingleton<IMarketWatchScanner>(sp =>
                new HouseVictoria.Services.Trading.MarketWatchScannerService(
                    sp.GetRequiredService<AppConfig>(),
                    sp.GetRequiredService<ITradingService>(),
                    sp.GetService<IProjectManagementService>()));

            // High-level cognitive agent service (composes AI + virtual environment)
            services.AddSingleton<IAgentService, AgentService>();
            services.AddSingleton<IJournalService, HouseVictoria.Services.Journals.JournalService>();
            services.AddSingleton<IAarService>(sp =>
                new HouseVictoria.Services.Aar.AarService(
                    sp.GetRequiredService<AppConfig>(),
                    sp.GetRequiredService<IProjectManagementService>(),
                    sp.GetService<IAIService>(),
                    sp.GetService<IMemoryService>(),
                    sp.GetService<DatabasePersistenceService>(),
                    sp.GetService<IPersonaContext>()));
            services.AddSingleton<IAutonomyService>(sp => new AutonomyOrchestratorService(
                sp.GetRequiredService<AppConfig>(),
                sp.GetRequiredService<IAIService>(),
                sp.GetRequiredService<DatabasePersistenceService>(),
                sp.GetRequiredService<IProjectManagementService>(),
                sp.GetRequiredService<IFileGenerationService>(),
                sp.GetService<IMemoryService>(),
                sp.GetService<IAgentService>(),
                sp.GetService<IVirtualEnvironmentService>(),
                sp.GetService<IJournalService>(),
                sp.GetService<ITradingService>(),
                sp.GetService<IMarketWatchScanner>(),
                sp.GetService<IPersonaContext>()));
            services.AddSingleton(sp => new RemoteCompanionChatService(
                sp.GetRequiredService<IAIService>(),
                sp.GetRequiredService<DatabasePersistenceService>(),
                sp.GetService<IMemoryService>(),
                sp.GetService<IVirtualEnvironmentService>(),
                sp.GetRequiredService<AppConfig>(),
                sp.GetService<IPersonaContext>()));

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
                HermesEndpoint = config["HermesEndpoint"] ?? "http://127.0.0.1:8642/v1",
                HermesApiKey = config["HermesApiKey"] ?? string.Empty,
                HermesModelName = string.IsNullOrWhiteSpace(config["HermesModelName"]) ? "hermes-agent" : (config["HermesModelName"] ?? "hermes-agent"),
                HermesAutoStart = !bool.TryParse(config["HermesAutoStart"], out var hermesAuto) || hermesAuto,
                MCPServerEndpoint = config["MCPServerEndpoint"] ?? "http://localhost:8080",
                UnrealEngineEndpoint = config["UnrealEngineEndpoint"] ?? "ws://localhost:8888",
                TTSEndpoint = config["TTSEndpoint"] ?? "http://localhost:8881",
                STTEndpoint = config["STTEndpoint"],
                ChatterboxVoicesDir = config["ChatterboxVoicesDir"] ?? "Media/ChatterboxVoices",
                UseWindowsTTSFallback = !bool.TryParse(config["UseWindowsTTSFallback"], out var noWinTts) || noWinTts,
                ImageGenerationProvider = string.IsNullOrWhiteSpace(config["ImageGenerationProvider"]) ? "a2e" : (config["ImageGenerationProvider"] ?? "a2e"),
                A2eApiToken = config["A2eApiToken"] ?? string.Empty,
                A2eApiBaseUrl = string.IsNullOrWhiteSpace(config["A2eApiBaseUrl"]) ? "https://video.a2e.ai" : (config["A2eApiBaseUrl"] ?? "https://video.a2e.ai"),
                StableDiffusionEndpoint = config["StableDiffusionEndpoint"] ?? "http://localhost:8188",
                ColorScheme = config["ColorScheme"] ?? "ObsidianFieldDark",
                StabilityMatrixPath = config["StabilityMatrixPath"] ?? string.Empty,
                ComfyUIPortablePath = config["ComfyUIPortablePath"] ?? string.Empty,
                ComfyUICustomWorkflowPath = config["ComfyUICustomWorkflowPath"] ?? string.Empty,
                ComfyUIPreferredCheckpoint = string.IsNullOrWhiteSpace(config["ComfyUIPreferredCheckpoint"]) ? "sd_xl_base_1.0.safetensors" : (config["ComfyUIPreferredCheckpoint"] ?? "sd_xl_base_1.0.safetensors"),
                MT4DataPath = config["MT4DataPath"] ?? "C:\\Program Files\\MetaTrader 4",
                DataBankPath = config["DataBankPath"] ?? "Data\\Databanks",
                LogsPath = config["LogsPath"] ?? "Logs",
                MediaPath = config["MediaPath"] ?? "Media",
                OperationalMode = !bool.TryParse(config["OperationalMode"], out var operationalMode) || operationalMode,
                EnableOverlay = bool.TryParse(config["EnableOverlay"], out var enableOverlay) && enableOverlay,
                AutoHideTrays = bool.TryParse(config["AutoHideTrays"], out var autoHideTrays) && autoHideTrays,
                EnablePgVector = bool.TryParse(config["EnablePgVector"], out var enablePg) && enablePg,
                PgVectorConnectionString = config["PgVectorConnectionString"],
                OllamaEmbeddingModel = string.IsNullOrWhiteSpace(config["OllamaEmbeddingModel"]) ? "nomic-embed-text" : (config["OllamaEmbeddingModel"] ?? "nomic-embed-text"),
                EmbeddingVectorDimensions = int.TryParse(config["EmbeddingVectorDimensions"], out var embDim) && embDim > 0 ? embDim : 768,
                HybridLexicalWeight = double.TryParse(config["HybridLexicalWeight"], out var hybridWeight) ? hybridWeight : 0.5,
                CovasBridgeEnabled = bool.TryParse(config["CovasBridgeEnabled"], out var covasEnabled) && covasEnabled,
                CovasBridgeEndpoint = config["CovasBridgeEndpoint"] ?? "http://localhost:11435",
                CovasContactId = config["CovasContactId"] ?? string.Empty,
                RemoteCompanionEnabled = bool.TryParse(config["RemoteCompanionEnabled"], out var rce) && rce,
                RemoteCompanionListenPort = int.TryParse(config["RemoteCompanionListenPort"], out var rcp) && rcp > 0 ? rcp : 17890,
                RemoteCompanionApiToken = config["RemoteCompanionApiToken"] ?? string.Empty,
                RemoteCompanionAiContactId = config["RemoteCompanionAiContactId"] ?? string.Empty,
                RemoteCompanionListenOnLan = bool.TryParse(config["RemoteCompanionListenOnLan"], out var rclan) && rclan,
                RemoteCompanionNotifyUnreal = bool.TryParse(config["RemoteCompanionNotifyUnreal"], out var rcnu) && rcnu,
                EnableAutonomy = !bool.TryParse(config["EnableAutonomy"], out var enableAutonomy) || enableAutonomy,
                AutonomyLevel = Enum.TryParse<AutonomyLevel>(config["AutonomyLevel"], true, out var autonomyLevel)
                    ? autonomyLevel
                    : AutonomyLevel.Mid,
                AutonomyTickIntervalSeconds = int.TryParse(config["AutonomyTickIntervalSeconds"], out var ati) && ati >= 30 ? ati : 90,
                AutonomyMinIdleMinutes = int.TryParse(config["AutonomyMinIdleMinutes"], out var ami) && ami >= 1 ? ami : 2,
                AutonomyHighPriorityThreshold = int.TryParse(config["AutonomyHighPriorityThreshold"], out var ahp) && ahp >= 1 ? ahp : 7,
                AutonomyAiContactId = config["AutonomyAiContactId"] ?? string.Empty,
                AutonomyEnableArtGeneration = !bool.TryParse(config["AutonomyEnableArtGeneration"], out var aag) || aag,
                AutonomyMaxActionsPerHour = int.TryParse(config["AutonomyMaxActionsPerHour"], out var ama) && ama > 0 ? ama : 8,
                AutonomyMaxArtPerHour = int.TryParse(config["AutonomyMaxArtPerHour"], out var amArt) && amArt > 0 ? amArt : 2,
                AutonomyDataPath = config["AutonomyDataPath"] ?? "Data/Autonomy",
                AutonomyEnableSelfGoals = !bool.TryParse(config["AutonomyEnableSelfGoals"], out var aesg) || aesg,
                AutonomyMaxSelfGoalsPerDay = int.TryParse(config["AutonomyMaxSelfGoalsPerDay"], out var amsg) && amsg >= 0 ? amsg : 3,
                AutonomySelfGoalDriveThreshold = double.TryParse(config["AutonomySelfGoalDriveThreshold"], out var asgdt) && asgdt is > 0 and <= 1 ? asgdt : 0.65,
                AutonomyMaxActiveSelfProjects = int.TryParse(config["AutonomyMaxActiveSelfProjects"], out var amasp) && amasp > 0 ? amasp : 3,
                AutonomyUserGuidanceMaxTicks = int.TryParse(config["AutonomyUserGuidanceMaxTicks"], out var augmt) && augmt > 0 ? augmt : 3,
                AutonomyMaxInterestTags = int.TryParse(config["AutonomyMaxInterestTags"], out var amit) && amit > 0 ? amit : 3,
                TradingWatchEnabled = !bool.TryParse(config["TradingWatchEnabled"], out var twe) || twe,
                TradingWatchSymbols = config["TradingWatchSymbols"] ?? string.Empty,
                TradingWatchIntervalSeconds = int.TryParse(config["TradingWatchIntervalSeconds"], out var twi) && twi >= 15 ? twi : 30,
                TradingWatchPipMoveThreshold = double.TryParse(config["TradingWatchPipMoveThreshold"], out var twp) && twp > 0 ? twp : 8,
                TradingWatchMaxSpreadPips = double.TryParse(config["TradingWatchMaxSpreadPips"], out var tws) && tws > 0 ? tws : 25,
                TradingWatchTechnicalEnabled = !bool.TryParse(config["TradingWatchTechnicalEnabled"], out var twte) || twte,
                TradingWatchTechnicalIntervalSeconds = int.TryParse(config["TradingWatchTechnicalIntervalSeconds"], out var twtis) && twtis >= 60 ? twtis : 300,
                TradingWatchTechnicalBarCount = int.TryParse(config["TradingWatchTechnicalBarCount"], out var twtbc) && twtbc >= 40 ? twtbc : 120,
                TradingWatchProjectPriority = int.TryParse(config["TradingWatchProjectPriority"], out var twpp) && twpp >= 1 ? twpp : 9,
                VoiceEngineEnabled = !bool.TryParse(config["VoiceEngineEnabled"], out var vee) || vee,
                VoiceEngineDirectory = config["VoiceEngineDirectory"] ?? string.Empty,
                VoiceEnginePython = config["VoiceEnginePython"] ?? string.Empty,
                VoiceEngineScript = string.IsNullOrWhiteSpace(config["VoiceEngineScript"]) ? "speech_to_speech.py" : config["VoiceEngineScript"]!,
                VoiceEngineVoice = string.IsNullOrWhiteSpace(config["VoiceEngineVoice"]) ? "default" : config["VoiceEngineVoice"]!,
                VoiceEngineShowConsole = !bool.TryParse(config["VoiceEngineShowConsole"], out var vesc) || vesc,
                VoiceEngineInputGain = float.TryParse(config["VoiceEngineInputGain"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var veg) && veg > 0 ? veg : 4f,
                VoiceEngineSilenceThreshold = float.TryParse(config["VoiceEngineSilenceThreshold"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vest) && vest > 0 ? vest : 0.003f,
                ChatMicRecordingGain = float.TryParse(config["ChatMicRecordingGain"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cmrg) && cmrg > 0 ? cmrg : 4f,
                RefreshIntervalMs = int.TryParse(config["RefreshIntervalMs"], out var refreshMs) && refreshMs > 0 ? refreshMs : 1000,
                OverlayOpacity = double.TryParse(config["OverlayOpacity"], out var overlayOpacity) ? overlayOpacity : 0.85,
                AutoHideDelayMs = int.TryParse(config["AutoHideDelayMs"], out var autoHideDelay) && autoHideDelay >= 0 ? autoHideDelay : 3000,
                WalkSpeed = double.TryParse(config["WalkSpeed"], out var walkSpeed) && walkSpeed > 0 ? walkSpeed : 1.0,
                RunSpeed = double.TryParse(config["RunSpeed"], out var runSpeed) && runSpeed > 0 ? runSpeed : 2.0,
                JumpHeight = double.TryParse(config["JumpHeight"], out var jumpHeight) && jumpHeight > 0 ? jumpHeight : 1.0,
                EnablePhysicsInteraction = !bool.TryParse(config["EnablePhysicsInteraction"], out var enablePhysics) || enablePhysics,
                EnableFileSystemAccess = !bool.TryParse(config["EnableFileSystemAccess"], out var enableFs) || enableFs,
                EnableNetworkAccess = !bool.TryParse(config["EnableNetworkAccess"], out var enableNet) || enableNet,
                EnableSystemCommands = bool.TryParse(config["EnableSystemCommands"], out var enableSysCmd) && enableSysCmd,
                EnablePersistentMemory = !bool.TryParse(config["EnablePersistentMemory"], out var enableMem) || enableMem,
                PersistentMemoryPath = config["PersistentMemoryPath"] ?? "Data/Memory",
                MemoryMaxEntries = int.TryParse(config["MemoryMaxEntries"], out var memMax) && memMax > 0 ? memMax : 10000,
                MemoryImportanceThreshold = double.TryParse(config["MemoryImportanceThreshold"], out var memThreshold) ? memThreshold : 0.5,
                MemoryRetentionDays = int.TryParse(config["MemoryRetentionDays"], out var memDays) && memDays > 0 ? memDays : 90
            };

            var userSettings = HouseVictoria.Core.Utils.UserSettingsStore.TryLoad();
            if (userSettings != null)
                HouseVictoria.Core.Utils.UserSettingsStore.MergeInto(appConfig, userSettings);

            // Resolve relative paths to absolute paths (prefer repo root so Debug/Release share one Data folder)
            var appDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            appConfig.DataBankPath = HouseVictoria.Core.Utils.AppDataRootResolver.ResolveDataPath(appDirectory, appConfig.DataBankPath);
            appConfig.LogsPath = HouseVictoria.Core.Utils.AppDataRootResolver.ResolveDataPath(appDirectory, appConfig.LogsPath);
            appConfig.PersistentMemoryPath = HouseVictoria.Core.Utils.AppDataRootResolver.ResolveDataPath(appDirectory, appConfig.PersistentMemoryPath);
            appConfig.AutonomyDataPath = HouseVictoria.Core.Utils.AppDataRootResolver.ResolveDataPath(appDirectory, appConfig.AutonomyDataPath);
            appConfig.MediaPath = System.IO.Path.IsPathRooted(appConfig.MediaPath)
                ? appConfig.MediaPath
                : System.IO.Path.Combine(HouseVictoria.Core.Utils.AppDataRootResolver.ResolveDataRoot(appDirectory), appConfig.MediaPath);
            if (!System.IO.Path.IsPathRooted(appConfig.ChatterboxVoicesDir))
            {
                var dataRoot = HouseVictoria.Core.Utils.AppDataRootResolver.ResolveDataRoot(appDirectory);
                appConfig.ChatterboxVoicesDir = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(dataRoot, appConfig.ChatterboxVoicesDir));
            }
            else
            {
                appConfig.ChatterboxVoicesDir = System.IO.Path.GetFullPath(appConfig.ChatterboxVoicesDir);
            }
            if (!System.IO.Directory.Exists(appConfig.ChatterboxVoicesDir))
                System.IO.Directory.CreateDirectory(appConfig.ChatterboxVoicesDir);

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
                if (_remoteCompanionHost != null)
                {
                    try
                    {
                        await _remoteCompanionHost.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error stopping remote companion host: {ex.Message}");
                    }

                    _remoteCompanionHost = null;
                }

                var autonomy = ServiceProvider?.GetService<IAutonomyService>();
                if (autonomy != null)
                {
                    try
                    {
                        await autonomy.StopAsync().ConfigureAwait(false);
                        System.Diagnostics.Debug.WriteLine("Autonomy service stopped");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error stopping autonomy service: {ex.Message}");
                    }
                }

                // Stop SystemMonitorService servers (includes COVAS bridge)
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

        /// <summary>
        /// <summary>
        /// One-time migration: persist role-appropriate knowledge-sharing defaults for personas
        /// created before per-persona sharing existed.
        /// </summary>
        private async Task SyncPersonaKnowledgeSharingAsync()
        {
            try
            {
                var persistence = ServiceProvider?.GetService<IPersistenceService>();
                if (persistence == null)
                    return;

                var contacts = await persistence.GetAllAsync<AIContact>().ConfigureAwait(false);
                foreach (var contact in contacts.Values)
                {
                    if (contact.KnowledgeSharing?.IsConfigured == true)
                        continue;

                    contact.KnowledgeSharing = PersonaKnowledgeSharing.Resolve(contact);
                    contact.KnowledgeSharing.IsConfigured = true;
                    await persistence.SetAsync($"AIContact_{contact.Id}", contact).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteToStartupLog($"SyncPersonaKnowledgeSharing error: {ex.Message}");
            }
        }

        /// Ensures every persisted persona points at the app MCP endpoint (memory + MT4 bridge tools).
        /// </summary>
        private async Task SyncPersonaMcpEndpointsAsync()
        {
            try
            {
                var persistence = ServiceProvider?.GetService<IPersistenceService>();
                var appConfig = ServiceProvider?.GetService<AppConfig>();
                if (persistence == null || appConfig == null)
                    return;

                var defaultMcp = appConfig.MCPServerEndpoint?.Trim();
                if (string.IsNullOrWhiteSpace(defaultMcp))
                    return;

                var contacts = await persistence.GetAllAsync<AIContact>().ConfigureAwait(false);
                foreach (var contact in contacts.Values)
                {
                    if (!string.IsNullOrWhiteSpace(contact.MCPServerEndpoint))
                        continue;

                    contact.MCPServerEndpoint = defaultMcp;
                    await persistence.SetAsync($"AIContact_{contact.Id}", contact).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(contact.DataPath))
                    {
                        var configPath = Path.Combine(contact.DataPath, "config.json");
                        try
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                contact.Id,
                                contact.Name,
                                contact.ModelName,
                                MCPServerEndpoint = defaultMcp,
                                contact.CreatedAt
                            });
                            await File.WriteAllTextAsync(configPath, json).ConfigureAwait(false);
                        }
                        catch (Exception fileEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Could not update persona config.json: {fileEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteToStartupLog($"SyncPersonaMcpEndpoints error: {ex.Message}");
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
