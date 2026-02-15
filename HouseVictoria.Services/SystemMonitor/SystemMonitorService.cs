using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.TTS;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace HouseVictoria.Services.SystemMonitor
{
    /// <summary>
    /// Service for monitoring system metrics and managing servers
    /// </summary>
    public class SystemMonitorService : ISystemMonitorService, IDisposable
    {
        private readonly Dictionary<string, ServerStatus> _serverStatuses = new();
        private readonly IMCPService? _mcpService;
        private readonly IVirtualEnvironmentService? _virtualEnvironmentService;
        private readonly AppConfig? _appConfig;
        private readonly CovasBridge.OpenAICompatibleBridge? _covasBridge;
        private readonly string _rootDirectory;
        private LocalTtsHttpHost? _localTtsHost;
        private VirtualEnvironmentStatus _cachedVirtualEnvironmentStatus = new();
        private System.Diagnostics.Process? _systemProcess;
        private DateTime _startTime;
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _memoryCounter;
        private List<PerformanceCounter>? _gpuCounters;
        private DateTime _lastCpuUpdate = DateTime.MinValue;
        private double _cachedCpuUsage = 0.0;
        private DateTime _lastGpuUpdate = DateTime.MinValue;
        private double _cachedGpuUsage = 0.0;
        private DateTime _lastMCPCheck = DateTime.MinValue;
        private readonly TimeSpan _mcpCheckInterval = TimeSpan.FromSeconds(30);
        private readonly HttpClient _httpClient;
        private readonly CancellationTokenSource _cts = new();
        
        // Hardware monitoring via WMI (Windows Management Instrumentation)
        // WMI doesn't require kernel drivers and won't trigger Windows Defender
        private DateTime _lastWmiUpdate = DateTime.MinValue;
        private readonly TimeSpan _wmiUpdateInterval = TimeSpan.FromSeconds(2); // Update WMI queries every 2 seconds
        private double? _cachedCpuTemperature = null;
        
        // NVML (NVIDIA Management Library) for GPU monitoring
        private DateTime _lastNvmlUpdate = DateTime.MinValue;
        private readonly TimeSpan _nvmlUpdateInterval = TimeSpan.FromSeconds(1); // Update NVML every 1 second
        private double? _cachedGpuTemperature = null;
        private double? _cachedGpuFanSpeed = null;
        private int _nvmlDeviceIndex = 0; // Use first NVIDIA GPU
        
        // Circuit breaker: Track consecutive failures per server to avoid hammering unreachable servers
        private readonly Dictionary<string, int> _consecutiveFailures = new();
        private readonly Dictionary<string, DateTime> _lastFailureTime = new();
        private readonly TimeSpan _circuitBreakerTimeout = TimeSpan.FromMinutes(1); // Don't retry for 1 minute after failures

        public event EventHandler<SystemMetricsUpdatedEventArgs>? MetricsUpdated;
        public event EventHandler<ServerStatusChangedEventArgs>? ServerStatusChanged;

        public SystemMonitorService(IMCPService? mcpService = null, IVirtualEnvironmentService? virtualEnvironmentService = null, AppConfig? appConfig = null, CovasBridge.OpenAICompatibleBridge? covasBridge = null)
        {
            _mcpService = mcpService;
            _virtualEnvironmentService = virtualEnvironmentService;
            _appConfig = appConfig;
            _covasBridge = covasBridge;
            _rootDirectory = LocateRootDirectory();
            _startTime = DateTime.Now;
            _systemProcess = Process.GetCurrentProcess();
            
            // Configure HttpClient with better settings for reliability
            var handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 5,
                UseCookies = false,
                AllowAutoRedirect = false
            };
            
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5),
                MaxResponseContentBufferSize = 512 * 1024 // 512KB
            };
            
            InitializeServers();
            ApplyConfigOverrides();
            InitializePerformanceCounters();
            // WMI-based hardware monitoring (no kernel drivers needed)
            // Note: WMI has limited temperature/fan speed support compared to WinRing0-based solutions
            
            // Initialize NVML for NVIDIA GPU monitoring (if available)
            InitializeNvml();

            _ = AutoStartCriticalServicesAsync();
            
            if (_mcpService != null)
            {
                _mcpService.ServerStatusChanged += OnMCPServerStatusChanged;
            }

            if (_virtualEnvironmentService != null)
            {
                _virtualEnvironmentService.StatusChanged += OnVirtualEnvironmentStatusChanged;
            }

            // Check all servers on startup
            _ = CheckAllServersOnStartupAsync();
        }

        private void OnVirtualEnvironmentStatusChanged(object? sender, VirtualEnvironmentEventArgs e)
        {
            // Cache the status for synchronous access
            _cachedVirtualEnvironmentStatus = e.Status;

            // Update server status for Unreal Engine
            if (_serverStatuses.TryGetValue("Unreal Engine", out var status))
            {
                var previousStatus = new ServerStatus
                {
                    Name = status.Name,
                    IsRunning = status.IsRunning,
                    Endpoint = status.Endpoint,
                    Uptime = status.Uptime
                };

                status.IsRunning = e.Status.IsConnected;
                status.Uptime = e.Status.Uptime;
                if (e.Status.IsConnected && status.LastStarted == null)
                {
                    status.LastStarted = DateTime.Now;
                }
                else if (!e.Status.IsConnected)
                {
                    status.LastStopped = DateTime.Now;
                }

                ServerStatusChanged?.Invoke(this, new ServerStatusChangedEventArgs
                {
                    ServerName = "Unreal Engine",
                    PreviousStatus = previousStatus,
                    CurrentStatus = status
                });
            }
        }

        private void InitializeNvml()
        {
            try
            {
                // NVML will initialize itself on first access via IsAvailable property
                if (NvmlWrapper.IsAvailable)
                {
                    var deviceCount = NvmlWrapper.GetDeviceCount();
                    if (deviceCount > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"NVML initialized: Found {deviceCount} NVIDIA GPU(s)");
                        _nvmlDeviceIndex = 0; // Use first GPU
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("NVML available but no NVIDIA GPUs found");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("NVML not available (NVIDIA drivers may not be installed)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing NVML: {ex.Message}");
            }
        }


        private async Task CheckAllServersOnStartupAsync()
        {
            // Wait a bit for the app to fully initialize
            await Task.Delay(1000).ConfigureAwait(false);

            try
            {
                if (_cts.IsCancellationRequested)
                    return;

                // Check all servers in parallel.
                // IMPORTANT: do NOT use ContinueWith(OnlyOnFaulted) here; that produces cancelled continuation-tasks
                // which then make Task.WhenAll throw TaskCanceledException even though we handled errors inside.
                var tasks = new List<Task>();

                if (_serverStatuses.TryGetValue("Ollama", out var ollamaStatus))
                {
                    tasks.Add(CheckOllamaServerAsync(ollamaStatus));
                }

                if (_serverStatuses.TryGetValue("MCP", out var mcpStatus) && _mcpService != null)
                {
                    tasks.Add(CheckMCPServerAsync(mcpStatus));
                }

                if (_serverStatuses.TryGetValue("TTS", out var ttsStatus))
                {
                    tasks.Add(CheckTTSServerAsync(ttsStatus));
                }

                if (_serverStatuses.TryGetValue("UnrealEngine", out var ueStatus))
                {
                    tasks.Add(CheckUnrealEngineServerAsync(ueStatus));
                }

                if (_serverStatuses.TryGetValue("ComfyUI", out var comfyUIStatus))
                {
                    tasks.Add(CheckComfyUIServerAsync(comfyUIStatus));
                }

                try
                {
                    // Wait for all tasks but don't let exceptions propagate
                    var completedTasks = tasks.Select(t => t.ContinueWith(
                        task =>
                        {
                            if (task.IsFaulted)
                            {
                                // Log but don't propagate - individual check methods handle errors
                                var ex = task.Exception?.GetBaseException();
                                if (ex != null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Server check task faulted: {ex.GetType().Name}: {ex.Message}");
                                }
                            }
                            return task.IsCompletedSuccessfully;
                        },
                        TaskContinuationOptions.ExecuteSynchronously)).ToArray();
                    
                    await Task.WhenAll(completedTasks).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // Expected when endpoints are down / timeouts occur; checks handle status updates.
                }
                catch (HttpRequestException ex)
                {
                    // Expected when servers are unreachable; checks handle status updates.
                    System.Diagnostics.Debug.WriteLine($"HTTP exception in server checks: {ex.Message}");
                }
                catch (System.Net.Sockets.SocketException ex)
                {
                    // Expected when servers are unreachable; checks handle status updates.
                    System.Diagnostics.Debug.WriteLine($"Socket exception in server checks: {ex.Message}");
                }
                catch (AggregateException ex)
                {
                    // Handle any aggregate exceptions from tasks
                    foreach (var innerEx in ex.Flatten().InnerExceptions)
                    {
                        System.Diagnostics.Debug.WriteLine($"Aggregate exception in server checks: {innerEx.GetType().Name}: {innerEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    // Catch any other unexpected exceptions
                    System.Diagnostics.Debug.WriteLine($"Unexpected exception in server checks: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                }
                System.Diagnostics.Debug.WriteLine("Initial server status check completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking servers on startup: {ex.Message}");
            }
        }

        private async Task AutoStartCriticalServicesAsync()
        {
            try
            {
                if (_serverStatuses.TryGetValue("MCP", out var mcpStatus))
                {
                    await StartMcpServerIfNeededAsync(mcpStatus).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-start MCP failed: {ex.Message}");
            }

            try
            {
                if (_serverStatuses.TryGetValue("TTS", out var ttsStatus))
                {
                    await StartTtsServiceIfNeededAsync(ttsStatus).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-start TTS failed: {ex.Message}");
            }

            try
            {
                if (_serverStatuses.TryGetValue("ComfyUI", out var comfyStatus))
                {
                    await StartComfyUiIfNeededAsync(comfyStatus).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-start ComfyUI failed: {ex.Message}");
            }

            try
            {
                if (_appConfig?.CovasBridgeEnabled == true && _covasBridge != null)
                {
                    await _covasBridge.StartAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine("COVAS bridge started for Elite Dangerous.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"COVAS bridge auto-start failed: {ex.Message}");
            }
        }

        private void OnMCPServerStatusChanged(object? sender, MCPServerStatusChangedEventArgs e)
        {
            if (_serverStatuses.TryGetValue("MCP", out var status))
            {
                var previousStatus = new ServerStatus
                {
                    Name = status.Name,
                    IsRunning = status.IsRunning,
                    Endpoint = status.Endpoint,
                    Uptime = status.Uptime,
                    LastStarted = status.LastStarted,
                    LastStopped = status.LastStopped,
                    Type = status.Type,
                    CpuUsage = status.CpuUsage,
                    MemoryUsage = status.MemoryUsage
                };

                status.IsRunning = e.IsAvailable;
                if (e.IsAvailable && !previousStatus.IsRunning)
                {
                    status.LastStarted = DateTime.Now;
                }
                else if (!e.IsAvailable && previousStatus.IsRunning)
                {
                    status.LastStopped = DateTime.Now;
                }

                ServerStatusChanged?.Invoke(this, new ServerStatusChangedEventArgs
                {
                    ServerName = "MCP",
                    PreviousStatus = previousStatus,
                    CurrentStatus = status
                });
            }
        }

        private string LocateRootDirectory()
        {
            try
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                int depth = 0;
                while (dir != null && depth < 6)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "HouseVictoria.sln")))
                    {
                        return dir.FullName;
                    }
                    dir = dir.Parent;
                    depth++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LocateRootDirectory failed: {ex.Message}");
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private void ApplyConfigOverrides()
        {
            if (_appConfig == null)
                return;

            if (_serverStatuses.TryGetValue("Ollama", out var ollamaStatus))
            {
                ollamaStatus.Endpoint = _appConfig.OllamaEndpoint;
            }

            if (_serverStatuses.TryGetValue("MCP", out var mcpStatus))
            {
                mcpStatus.Endpoint = _appConfig.MCPServerEndpoint;
            }

            if (_serverStatuses.TryGetValue("TTS", out var ttsStatus))
            {
                ttsStatus.Endpoint = _appConfig.TTSEndpoint;
            }

            if (_serverStatuses.TryGetValue("ComfyUI", out var comfyStatus))
            {
                comfyStatus.Endpoint = _appConfig.StableDiffusionEndpoint;
            }
        }

        private void InitializePerformanceCounters()
        {
            try
            {
                // Initialize CPU counter once and cache it
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // First call always returns 0
                
                // Initialize memory counter
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                
                // Initialize GPU counters
                InitializeGPUCounters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize performance counters: {ex.Message}");
            }
        }

        private void InitializeGPUCounters()
        {
            try
            {
                _gpuCounters = new List<PerformanceCounter>();
                
                // Try to get GPU usage via Performance Counters
                // On Windows 10/11, GPU metrics are available via "GPU Engine" category
                var categoryNames = new[] { "GPU Engine", "GPU Adapter Engine" };
                
                foreach (var categoryName in categoryNames)
                {
                    try
                    {
                        if (PerformanceCounterCategory.Exists(categoryName))
                        {
                            var category = new PerformanceCounterCategory(categoryName);
                            var instanceNames = category.GetInstanceNames();
                            
                            // Find instances related to GPU usage (typically contain "engtype_3D" or "engtype_VideoDecode" or "engtype_VideoEncode")
                            // We want to track 3D engine usage primarily
                            var gpuInstances = instanceNames.Where(name => 
                                name.Contains("engtype_3D") || 
                                name.Contains("engtype_VideoDecode") ||
                                name.Contains("engtype_VideoEncode") ||
                                name.Contains("engtype_Compute")).ToList();
                            
                            if (gpuInstances.Count == 0)
                            {
                                // Fallback: try all instances and sum them
                                gpuInstances = instanceNames.ToList();
                            }
                            
                            foreach (var instanceName in gpuInstances)
                            {
                                try
                                {
                                    // Try "Utilization Percentage" counter first, then "Dedicated Usage" or "Shared Usage"
                                    var counterNames = new[] { "Utilization Percentage", "Dedicated Usage", "Shared Usage", "% Utilization" };
                                    foreach (var counterName in counterNames)
                                    {
                                        try
                                        {
                                            if (category.CounterExists(counterName))
                                            {
                                                var counter = new PerformanceCounter(categoryName, counterName, instanceName, true);
                                                counter.NextValue(); // First call always returns 0
                                                _gpuCounters.Add(counter);
                                                System.Diagnostics.Debug.WriteLine($"Initialized GPU counter: {categoryName}\\{counterName}\\{instanceName}");
                                            }
                                        }
                                        catch
                                        {
                                            // Counter doesn't exist for this instance, try next
                                            continue;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to create GPU counter for instance {instanceName}: {ex.Message}");
                                }
                            }
                            
                            if (_gpuCounters.Count > 0)
                            {
                                break; // Found working GPU counters, exit loop
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"GPU category {categoryName} not available: {ex.Message}");
                        continue;
                    }
                }
                
                if (_gpuCounters.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No GPU performance counters found. GPU usage will show 0%.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize GPU counters: {ex.Message}");
                _gpuCounters = null;
            }
        }

        private void InitializeServers()
        {
            // Initialize default servers
            _serverStatuses["Ollama"] = new ServerStatus
            {
                Name = "Ollama",
                IsRunning = false,
                Endpoint = "http://localhost:11434",
                Type = ServerType.LLM
            };

            _serverStatuses["MCP"] = new ServerStatus
            {
                Name = "MCP Server",
                IsRunning = false,
                Endpoint = "http://localhost:8080",
                Type = ServerType.MCP
            };

            _serverStatuses["UnrealEngine"] = new ServerStatus
            {
                Name = "Unreal Engine",
                IsRunning = false,
                Endpoint = "ws://localhost:8888",
                Type = ServerType.UnrealEngine
            };

            _serverStatuses["TTS"] = new ServerStatus
            {
                Name = "TTS Service",
                IsRunning = false,
                Endpoint = "http://localhost:5000",
                Type = ServerType.TTS
            };

            _serverStatuses["DataBank"] = new ServerStatus
            {
                Name = "Data Bank",
                IsRunning = true,
                Type = ServerType.DataBank
            };

            _serverStatuses["ComfyUI"] = new ServerStatus
            {
                Name = "ComfyUI",
                IsRunning = false,
                Endpoint = "http://localhost:8188",
                Type = ServerType.Other
            };
        }

        public SystemMetrics GetCurrentMetrics()
        {
            var metrics = new SystemMetrics
            {
                CPUUsage = GetCPUUsage(),
                CPUTemperature = GetCPUTemperature(),
                CPUFanSpeed = GetCPUFanSpeed(),
                GPUUsage = GetGPUUsage(),
                GPUTemperature = GetGPUTemperature(),
                GPUFanSpeed = GetGPUFanSpeed(),
                RAMUsed = GetRAMUsed(),
                RAMTotal = GetRAMTotal(),
                Timestamp = DateTime.Now
            };

            MetricsUpdated?.Invoke(this, new SystemMetricsUpdatedEventArgs { Metrics = metrics });
            return metrics;
        }

        public async Task<ServerStatus> GetServerStatusAsync(string serverName)
        {
            if (_serverStatuses.TryGetValue(serverName, out var status))
            {
                // For MCP server, check actual status if service is available
                if (serverName == "MCP" && _mcpService != null && 
                    !string.IsNullOrWhiteSpace(status.Endpoint) &&
                    (DateTime.Now - _lastMCPCheck) > _mcpCheckInterval)
                {
                    try
                    {
                        var isAvailable = await _mcpService.IsServerAvailableAsync(status.Endpoint);
                        if (status.IsRunning != isAvailable)
                        {
                            var previousStatus = new ServerStatus
                            {
                                Name = status.Name,
                                IsRunning = status.IsRunning,
                                Endpoint = status.Endpoint,
                                Uptime = status.Uptime,
                                LastStarted = status.LastStarted,
                                LastStopped = status.LastStopped,
                                Type = status.Type,
                                CpuUsage = status.CpuUsage,
                                MemoryUsage = status.MemoryUsage
                            };

                            status.IsRunning = isAvailable;
                            if (isAvailable && !previousStatus.IsRunning)
                            {
                                status.LastStarted = DateTime.Now;
                            }
                            else if (!isAvailable && previousStatus.IsRunning)
                            {
                                status.LastStopped = DateTime.Now;
                            }

                            ServerStatusChanged?.Invoke(this, new ServerStatusChangedEventArgs
                            {
                                ServerName = "MCP",
                                PreviousStatus = previousStatus,
                                CurrentStatus = status
                            });
                        }
                        _lastMCPCheck = DateTime.Now;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error checking MCP server status: {ex.Message}");
                    }
                }

                // Calculate uptime
                status.Uptime = status.IsRunning && status.LastStarted.HasValue
                    ? DateTime.Now - status.LastStarted.Value
                    : TimeSpan.Zero;

                // Return a copy to avoid with expression issues
                var copy = new ServerStatus
                {
                    Name = status.Name,
                    IsRunning = status.IsRunning,
                    Endpoint = status.Endpoint,
                    Uptime = status.Uptime,
                    LastStarted = status.LastStarted,
                    LastStopped = status.LastStopped,
                    Type = status.Type,
                    CpuUsage = status.CpuUsage,
                    MemoryUsage = status.MemoryUsage
                };
                return copy;
            }
            throw new KeyNotFoundException($"Server '{serverName}' not found");
        }

        public async Task<Dictionary<string, ServerStatus>> GetAllServerStatusesAsync()
        {
            // Refresh statuses
            foreach (var server in _serverStatuses.Values)
            {
                server.Uptime = server.IsRunning && server.LastStarted.HasValue
                    ? DateTime.Now - server.LastStarted.Value
                    : TimeSpan.Zero;
            }
            // Return a copy
            var copy = new Dictionary<string, ServerStatus>();
            foreach (var kvp in _serverStatuses)
            {
                copy[kvp.Key] = new ServerStatus
                {
                    Name = kvp.Value.Name,
                    IsRunning = kvp.Value.IsRunning,
                    Endpoint = kvp.Value.Endpoint,
                    Uptime = kvp.Value.Uptime,
                    LastStarted = kvp.Value.LastStarted,
                    LastStopped = kvp.Value.LastStopped,
                    Type = kvp.Value.Type,
                    CpuUsage = kvp.Value.CpuUsage,
                    MemoryUsage = kvp.Value.MemoryUsage
                };
            }
            return await Task.FromResult(copy);
        }

        public async Task RestartServerAsync(string serverName)
        {
            await StopServerAsync(serverName);
            await Task.Delay(1000); // Wait before starting
            await StartServerAsync(serverName);
        }

        public async Task StopServerAsync(string serverName)
        {
            if (_serverStatuses.TryGetValue(serverName, out var status))
            {
                var previousStatus = new ServerStatus
                {
                    Name = status.Name,
                    IsRunning = status.IsRunning,
                    Endpoint = status.Endpoint,
                    Uptime = status.Uptime,
                    LastStarted = status.LastStarted,
                    LastStopped = status.LastStopped,
                    Type = status.Type,
                    CpuUsage = status.CpuUsage,
                    MemoryUsage = status.MemoryUsage
                };
                status.IsRunning = false;
                status.LastStopped = DateTime.Now;
                ServerStatusChanged?.Invoke(this, new ServerStatusChangedEventArgs
                {
                    ServerName = serverName,
                    PreviousStatus = previousStatus,
                    CurrentStatus = status
                });
            }
            await Task.CompletedTask;
        }

        public async Task StartServerAsync(string serverName)
        {
            if (_serverStatuses.TryGetValue(serverName, out var status))
            {
                var previousStatus = new ServerStatus
                {
                    Name = status.Name,
                    IsRunning = status.IsRunning,
                    Endpoint = status.Endpoint,
                    Uptime = status.Uptime,
                    LastStarted = status.LastStarted,
                    LastStopped = status.LastStopped,
                    Type = status.Type,
                    CpuUsage = status.CpuUsage,
                    MemoryUsage = status.MemoryUsage
                };
                status.IsRunning = true;
                status.LastStarted = DateTime.Now;
                ServerStatusChanged?.Invoke(this, new ServerStatusChangedEventArgs
                {
                    ServerName = serverName,
                    PreviousStatus = previousStatus,
                    CurrentStatus = status
                });
            }
            await Task.CompletedTask;
        }

        public async Task ShutdownAllServersAsync()
        {
            foreach (var serverName in _serverStatuses.Keys.ToList())
            {
                await StopServerAsync(serverName);
            }

            if (_localTtsHost != null)
            {
                try
                {
                    await _localTtsHost.StopAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error stopping embedded TTS host: {ex.Message}");
                }
            }
        }

        public TimeSpan GetSystemUptime()
        {
            return DateTime.Now - _startTime;
        }

        public AIStatus GetPrimaryAIStatus()
        {
            return new AIStatus
            {
                Id = "primary",
                Name = "Primary AI",
                IsActive = true,
                IsLoaded = true,
                CurrentTask = "Monitoring system",
                LastActivity = DateTime.Now
            };
        }

        public AIStatus GetCurrentAIContactStatus()
        {
            return new AIStatus
            {
                Id = "current",
                Name = "Current AI Contact",
                IsActive = false,
                IsLoaded = false,
                LastActivity = DateTime.Now
            };
        }

        public VirtualEnvironmentStatus GetVirtualEnvironmentStatus()
        {
            if (_virtualEnvironmentService != null)
            {
                try
                {
                    // Get status asynchronously but return cached status synchronously
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _virtualEnvironmentService.GetStatusAsync();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error getting Virtual Environment status: {ex.Message}");
                        }
                    });

                    // Return cached status (updated via StatusChanged event)
                    return _cachedVirtualEnvironmentStatus;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetVirtualEnvironmentStatus: {ex.Message}");
                }
            }

            return _cachedVirtualEnvironmentStatus;
        }

        // Helper methods for getting system metrics
        private double GetCPUUsage()
        {
            try
            {
                if (_cpuCounter == null)
                {
                    InitializePerformanceCounters();
                    return 0.0;
                }

                // Only update CPU usage every 500ms to avoid blocking
                var timeSinceLastUpdate = DateTime.Now - _lastCpuUpdate;
                if (timeSinceLastUpdate.TotalMilliseconds < 500)
                {
                    return _cachedCpuUsage;
                }

                // Get CPU usage without blocking
                var cpuUsage = _cpuCounter.NextValue();
                _cachedCpuUsage = cpuUsage;
                _lastCpuUpdate = DateTime.Now;
                return cpuUsage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting CPU usage: {ex.Message}");
                return 0.0;
            }
        }

        private double GetCPUTemperature()
        {
            try
            {
                // Use WMI to get CPU temperature (limited support - not all CPUs expose this)
                // MSAcpi_ThermalZoneTemperature provides temperature in tenths of a degree Kelvin
                var timeSinceLastUpdate = DateTime.Now - _lastWmiUpdate;
                if (timeSinceLastUpdate < _wmiUpdateInterval && _cachedCpuTemperature.HasValue)
                {
                    return _cachedCpuTemperature.Value;
                }

                using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var currentTemp = obj["CurrentTemperature"];
                    if (currentTemp != null)
                    {
                        // Temperature is in tenths of a degree Kelvin, convert to Celsius
                        var tempKelvin = Convert.ToDouble(currentTemp) / 10.0;
                        var tempCelsius = tempKelvin - 273.15;
                        _cachedCpuTemperature = tempCelsius;
                        _lastWmiUpdate = DateTime.Now;
                        return tempCelsius;
                    }
                }
            }
            catch (ManagementException ex)
            {
                // WMI query may fail if thermal zone not available (common on many systems)
                System.Diagnostics.Debug.WriteLine($"WMI CPU temperature query failed (may not be supported on this system): {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting CPU temperature via WMI: {ex.Message}");
            }
            return 0.0;
        }

        private double GetCPUFanSpeed()
        {
            // WMI does not provide CPU fan speed information
            // This would require kernel-level driver access (WinRing0) which triggers Windows Defender
            // Returning 0.0 to indicate data not available
            return 0.0;
        }

        private double GetGPUUsage()
        {
            try
            {
                // Try NVML first for NVIDIA GPUs (more accurate)
                if (NvmlWrapper.IsAvailable)
                {
                    var timeSinceLastUpdate = DateTime.Now - _lastGpuUpdate;
                    if (timeSinceLastUpdate.TotalMilliseconds < 500 && _cachedGpuUsage > 0)
                    {
                        return _cachedGpuUsage;
                    }

                    var nvmlUsage = NvmlWrapper.GetUtilization(_nvmlDeviceIndex);
                    if (nvmlUsage > 0)
                    {
                        _cachedGpuUsage = nvmlUsage;
                        _lastGpuUpdate = DateTime.Now;
                        return nvmlUsage;
                    }
                }

                // Fallback to Performance Counters
                var timeSinceLastUpdate2 = DateTime.Now - _lastGpuUpdate;
                if (timeSinceLastUpdate2.TotalMilliseconds < 500 && _cachedGpuUsage > 0)
                {
                    return _cachedGpuUsage;
                }

                if (_gpuCounters == null || _gpuCounters.Count == 0)
                {
                    // Try to initialize GPU counters if not already done
                    if (_gpuCounters == null)
                    {
                        InitializeGPUCounters();
                    }
                    
                    if (_gpuCounters == null || _gpuCounters.Count == 0)
                    {
                        return 0.0; // No GPU counters available
                    }
                }

                // Sum up GPU usage from all counters (multiple engines may be active)
                double totalUsage = 0.0;
                int validCounters = 0;
                
                foreach (var counter in _gpuCounters)
                {
                    try
                    {
                        var value = counter.NextValue();
                        if (!double.IsNaN(value) && value >= 0 && value <= 100)
                        {
                            totalUsage += value;
                            validCounters++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reading GPU counter: {ex.Message}");
                        // Counter may have become invalid, continue with others
                    }
                }

                if (validCounters > 0)
                {
                    // Average the usage across all counters, or take max - let's use max as it's more representative
                    // of actual GPU load (one engine at 100% means GPU is fully utilized)
                    var gpuUsage = totalUsage / validCounters;
                    
                    // Clamp to 0-100 range
                    gpuUsage = Math.Max(0, Math.Min(100, gpuUsage));
                    
                    _cachedGpuUsage = gpuUsage;
                    _lastGpuUpdate = DateTime.Now;
                    return gpuUsage;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting GPU usage: {ex.Message}");
            }
            return 0.0;
        }

        private double GetGPUTemperature()
        {
            try
            {
                // Use NVML for NVIDIA GPU temperature (if available)
                if (NvmlWrapper.IsAvailable)
                {
                    var timeSinceLastUpdate = DateTime.Now - _lastNvmlUpdate;
                    if (timeSinceLastUpdate < _nvmlUpdateInterval && _cachedGpuTemperature.HasValue)
                    {
                        return _cachedGpuTemperature.Value;
                    }

                    var temperature = NvmlWrapper.GetTemperature(_nvmlDeviceIndex);
                    if (temperature > 0)
                    {
                        _cachedGpuTemperature = temperature;
                        _lastNvmlUpdate = DateTime.Now;
                        return temperature;
                    }
                }
                
                // Fallback: WMI does not provide GPU temperature information
                // Returning 0.0 to indicate data not available
                return 0.0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting GPU temperature: {ex.Message}");
                return 0.0;
            }
        }

        private double GetGPUFanSpeed()
        {
            try
            {
                // Use NVML for NVIDIA GPU fan speed (if available)
                if (NvmlWrapper.IsAvailable)
                {
                    var timeSinceLastUpdate = DateTime.Now - _lastNvmlUpdate;
                    if (timeSinceLastUpdate < _nvmlUpdateInterval && _cachedGpuFanSpeed.HasValue)
                    {
                        return _cachedGpuFanSpeed.Value;
                    }

                    // Try to get RPM first, fallback to percentage if RPM not available
                    var fanSpeedRpm = NvmlWrapper.GetFanSpeedRpm(_nvmlDeviceIndex);
                    if (fanSpeedRpm > 0)
                    {
                        _cachedGpuFanSpeed = fanSpeedRpm;
                        _lastNvmlUpdate = DateTime.Now;
                        return fanSpeedRpm;
                    }

                    // If RPM not available, try percentage and convert to approximate RPM
                    // Most GPUs report fan speed as percentage (0-100, sometimes can exceed 100)
                    var fanSpeedPercent = NvmlWrapper.GetFanSpeedPercentage(_nvmlDeviceIndex);
                    if (fanSpeedPercent > 0)
                    {
                        // Convert percentage to approximate RPM
                        // Typical max GPU fan speed is around 3000-4000 RPM for most consumer GPUs
                        // High-end GPUs may go up to 5000+ RPM
                        // Using 3500 RPM as a reasonable estimate for conversion
                        // Clamp percentage to 100% max for conversion (some GPUs report >100%)
                        var clampedPercent = Math.Min(100, fanSpeedPercent);
                        var estimatedRpm = (clampedPercent / 100.0) * 3500.0;
                        _cachedGpuFanSpeed = estimatedRpm;
                        _lastNvmlUpdate = DateTime.Now;
                        return estimatedRpm;
                    }
                }
                
                // Fallback: WMI does not provide GPU fan speed information
                // Returning 0.0 to indicate data not available
                return 0.0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting GPU fan speed: {ex.Message}");
                return 0.0;
            }
        }

        private long GetRAMUsed()
        {
            try
            {
                if (_memoryCounter == null)
                {
                    InitializePerformanceCounters();
                    return 0;
                }

                var available = _memoryCounter.NextValue();
                // Get total using GC or another method
                var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
                return (long)(total - available);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting RAM usage: {ex.Message}");
                return 0;
            }
        }

        private long GetRAMTotal()
        {
            // Get total available memory
            return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
        }

        private async Task StartMcpServerIfNeededAsync(ServerStatus status)
        {
            if (_mcpService == null)
                return;

            var endpoint = _appConfig?.MCPServerEndpoint ?? status.Endpoint ?? "http://localhost:8080";
            status.Endpoint = endpoint;

            try
            {
                var isRunning = await _mcpService.IsServerAvailableAsync(endpoint).ConfigureAwait(false);
                if (isRunning)
                {
                    UpdateServerStatus(status, true, "MCP");
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Initial MCP availability check failed: {ex.Message}");
            }

            try
            {
                var mcpDir = Path.Combine(_rootDirectory, "MCPServer");
                var serverPath = Path.Combine(mcpDir, "http_server.py");
                if (!File.Exists(serverPath))
                {
                    System.Diagnostics.Debug.WriteLine($"MCP server script not found at {serverPath}");
                    UpdateServerStatus(status, false, "MCP");
                    return;
                }

                var venvPython = Path.Combine(mcpDir, ".venv", "Scripts", "python.exe");
                var fileName = File.Exists(venvPython) ? venvPython : "python";

                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = $"\"{serverPath}\"",
                    WorkingDirectory = mcpDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(startInfo);
                await Task.Delay(2000).ConfigureAwait(false);

                var isNowRunning = await _mcpService.IsServerAvailableAsync(endpoint).ConfigureAwait(false);
                UpdateServerStatus(status, isNowRunning, "MCP");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start MCP server: {ex.Message}");
                UpdateServerStatus(status, false, "MCP");
            }
        }

        private async Task StartTtsServiceIfNeededAsync(ServerStatus status)
        {
            var endpoint = _appConfig?.TTSEndpoint ?? status.Endpoint ?? "http://localhost:5000";
            status.Endpoint = endpoint;

            if (await IsTtsHealthyAsync(endpoint).ConfigureAwait(false))
            {
                UpdateServerStatus(status, true, "TTS");
                return;
            }

            try
            {
                _localTtsHost ??= new LocalTtsHttpHost(endpoint);
                await _localTtsHost.StartAsync().ConfigureAwait(false);

                await Task.Delay(300).ConfigureAwait(false);
                var isRunning = await IsTtsHealthyAsync(endpoint).ConfigureAwait(false);
                UpdateServerStatus(status, isRunning, "TTS");
            }
            catch (HttpListenerException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Embedded TTS failed to start: {ex.Message}");
                UpdateServerStatus(status, false, "TTS");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start embedded TTS service: {ex.Message}");
                UpdateServerStatus(status, false, "TTS");
            }
        }

        private async Task StartComfyUiIfNeededAsync(ServerStatus status)
        {
            var endpoint = _appConfig?.StableDiffusionEndpoint ?? status.Endpoint ?? "http://localhost:8188";
            status.Endpoint = endpoint;

            if (await IsComfyUiHealthyAsync(endpoint).ConfigureAwait(false))
            {
                UpdateServerStatus(status, true, "ComfyUI");
                return;
            }

            var candidates = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ComfyUI", "ComfyUI.exe"),
                @"C:\StabilityMatrix\Data\Packages\ComfyUI\main.py",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ComfyUI", "main.py"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ComfyUI", "main.py")
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    if (!File.Exists(candidate))
                        continue;

                    Process? process = null;
                    if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        process = Process.Start(new ProcessStartInfo
                        {
                            FileName = candidate,
                            WorkingDirectory = Path.GetDirectoryName(candidate) ?? _rootDirectory,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        var workingDir = Path.GetDirectoryName(candidate) ?? _rootDirectory;
                        process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "python",
                            Arguments = $"\"{candidate}\" --port 8188",
                            WorkingDirectory = workingDir,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                    }

                    if (process != null)
                    {
                        await Task.Delay(3000).ConfigureAwait(false);
                        if (await IsComfyUiHealthyAsync(endpoint).ConfigureAwait(false))
                        {
                            UpdateServerStatus(status, true, "ComfyUI");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to start ComfyUI from {candidate}: {ex.Message}");
                }
            }

            UpdateServerStatus(status, false, "ComfyUI");
        }

        private async Task<bool> IsTtsHealthyAsync(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return false;

            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);
                var baseUrl = endpoint.TrimEnd('/');
                var response = await _httpClient.GetAsync($"{baseUrl}/health", linked.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return true;

                response = await _httpClient.GetAsync($"{baseUrl}/", linked.Token).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsComfyUiHealthyAsync(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return false;

            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);
                var response = await _httpClient.GetAsync($"{endpoint.TrimEnd('/')}/system_stats", linked.Token).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task CheckOllamaServerAsync(ServerStatus status)
        {
            // Circuit breaker: Skip check if server has failed recently
            if (ShouldSkipCheck("Ollama", status.Endpoint))
            {
                UpdateServerStatus(status, false, "Ollama");
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(status.Endpoint))
                    return;

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);
                
                try
                {
                    // Use Task.Run to ensure exception is observed even if it happens in background
                    var response = await Task.Run(async () => 
                        await _httpClient.GetAsync($"{status.Endpoint}/api/tags", linked.Token).ConfigureAwait(false),
                        linked.Token).ConfigureAwait(false);
                    
                    var isRunning = response.IsSuccessStatusCode;
                    UpdateServerStatus(status, isRunning, "Ollama");
                    
                    // Reset failure count on success
                    if (isRunning)
                    {
                        _consecutiveFailures["Ollama"] = 0;
                    }
                }
                catch (TaskCanceledException) when (timeoutCts.Token.IsCancellationRequested || _cts.Token.IsCancellationRequested)
                {
                    // Expected timeout or cancellation
                    RecordFailure("Ollama");
                    UpdateServerStatus(status, false, "Ollama");
                }
                catch (System.Net.Http.HttpRequestException)
                {
                    // Connection failed
                    RecordFailure("Ollama");
                    UpdateServerStatus(status, false, "Ollama");
                }
                catch (System.Net.Sockets.SocketException)
                {
                    // Socket error
                    RecordFailure("Ollama");
                    UpdateServerStatus(status, false, "Ollama");
                }
                catch (Exception ex)
                {
                    // Log and mark as offline
                    RecordFailure("Ollama");
                    System.Diagnostics.Debug.WriteLine($"Ollama check error: {ex.GetType().Name} - {ex.Message}");
                    UpdateServerStatus(status, false, "Ollama");
                }
            }
            catch (Exception ex)
            {
                // Outer catch for any unexpected errors
                RecordFailure("Ollama");
                System.Diagnostics.Debug.WriteLine($"Ollama health check outer error: {ex.GetType().Name} - {ex.Message}");
                UpdateServerStatus(status, false, "Ollama");
            }
        }

        private bool ShouldSkipCheck(string serverName, string? endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return true;
                
            // Check if we should skip due to circuit breaker
            if (_consecutiveFailures.TryGetValue(serverName, out var failures) && failures >= 3)
            {
                if (_lastFailureTime.TryGetValue(serverName, out var lastFailure))
                {
                    if (DateTime.Now - lastFailure < _circuitBreakerTimeout)
                    {
                        // Skip check - server is likely down, don't hammer it
                        return true;
                    }
                    else
                    {
                        // Timeout expired, reset and try again
                        _consecutiveFailures[serverName] = 0;
                    }
                }
            }
            return false;
        }

        private void RecordFailure(string serverName)
        {
            _consecutiveFailures.TryGetValue(serverName, out var failures);
            _consecutiveFailures[serverName] = failures + 1;
            _lastFailureTime[serverName] = DateTime.Now;
        }

        private async Task CheckMCPServerAsync(ServerStatus status)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(status.Endpoint) || _mcpService == null)
                    return;

                if (_cts.IsCancellationRequested)
                    return;
                var isRunning = await _mcpService.IsServerAvailableAsync(status.Endpoint).ConfigureAwait(false);
                UpdateServerStatus(status, isRunning, "MCP");
            }
            catch (TaskCanceledException)
            {
                // Expected when server is down or timeout occurs
                UpdateServerStatus(status, false, "MCP");
            }
            catch (HttpRequestException)
            {
                // Expected when server is unreachable
                UpdateServerStatus(status, false, "MCP");
            }
            catch (System.Net.Sockets.SocketException)
            {
                // Expected when server is unreachable
                UpdateServerStatus(status, false, "MCP");
            }
            catch (Exception ex)
            {
                // Only log unexpected exceptions
                System.Diagnostics.Debug.WriteLine($"MCP health check error: {ex.Message}");
                UpdateServerStatus(status, false, "MCP");
            }
        }

        private async Task CheckTTSServerAsync(ServerStatus status)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(status.Endpoint))
                    return;

                // Try /health endpoint first, then root
                var endpoints = new[] { "/health", "/" };
                bool isRunning = false;

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);
                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync($"{status.Endpoint}{endpoint}", linked.Token).ConfigureAwait(false);
                        if (response.IsSuccessStatusCode)
                        {
                            isRunning = true;
                            break;
                        }
                    }
                    catch (TaskCanceledException) when (timeoutCts.Token.IsCancellationRequested || _cts.Token.IsCancellationRequested)
                    {
                        // Expected timeout or cancellation - try next endpoint
                        continue;
                    }
                    catch (System.Net.Http.HttpRequestException)
                    {
                        // Connection failed - try next endpoint
                        continue;
                    }
                    catch (System.Net.Sockets.SocketException)
                    {
                        // Socket error - try next endpoint
                        continue;
                    }
                    catch (Exception ex)
                    {
                        // Log unexpected exceptions but continue
                        System.Diagnostics.Debug.WriteLine($"TTS check error on {endpoint}: {ex.GetType().Name} - {ex.Message}");
                        continue;
                    }
                }

                UpdateServerStatus(status, isRunning, "TTS");
            }
            catch (TaskCanceledException)
            {
                // Expected when server is down or timeout occurs
                UpdateServerStatus(status, false, "TTS");
            }
            catch (HttpRequestException)
            {
                // Expected when server is unreachable
                UpdateServerStatus(status, false, "TTS");
            }
            catch (System.Net.Sockets.SocketException)
            {
                // Expected when server is unreachable
                UpdateServerStatus(status, false, "TTS");
            }
            catch (Exception ex)
            {
                // Only log unexpected exceptions
                System.Diagnostics.Debug.WriteLine($"TTS health check error: {ex.Message}");
                UpdateServerStatus(status, false, "TTS");
            }
        }

        private async Task CheckUnrealEngineServerAsync(ServerStatus status)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(status.Endpoint))
                    return;

                // Skip network probing to avoid socket exceptions during startup/dispose
                UpdateServerStatus(status, false, "UnrealEngine");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UnrealEngine health check error: {ex.Message}");
                UpdateServerStatus(status, false, "UnrealEngine");
            }
        }

        private async Task CheckComfyUIServerAsync(ServerStatus status)
        {
            // Circuit breaker: Skip check if server has failed recently
            if (ShouldSkipCheck("ComfyUI", status.Endpoint))
            {
                UpdateServerStatus(status, false, "ComfyUI");
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(status.Endpoint))
                    return;

                // ComfyUI uses /system_stats endpoint for health checks
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);
                
                try
                {
                    // Use Task.Run to ensure exception is observed even if it happens in background
                    var response = await Task.Run(async () => 
                        await _httpClient.GetAsync($"{status.Endpoint}/system_stats", linked.Token).ConfigureAwait(false),
                        linked.Token).ConfigureAwait(false);
                    
                    var isRunning = response.IsSuccessStatusCode;
                    UpdateServerStatus(status, isRunning, "ComfyUI");
                    
                    // Reset failure count on success
                    if (isRunning)
                    {
                        _consecutiveFailures["ComfyUI"] = 0;
                    }
                }
                catch (TaskCanceledException) when (timeoutCts.Token.IsCancellationRequested || _cts.Token.IsCancellationRequested)
                {
                    // Expected timeout or cancellation
                    RecordFailure("ComfyUI");
                    UpdateServerStatus(status, false, "ComfyUI");
                }
                catch (System.Net.Http.HttpRequestException)
                {
                    // Connection failed
                    RecordFailure("ComfyUI");
                    UpdateServerStatus(status, false, "ComfyUI");
                }
                catch (System.Net.Sockets.SocketException)
                {
                    // Socket error
                    RecordFailure("ComfyUI");
                    UpdateServerStatus(status, false, "ComfyUI");
                }
                catch (Exception ex)
                {
                    // Log and mark as offline
                    RecordFailure("ComfyUI");
                    System.Diagnostics.Debug.WriteLine($"ComfyUI check error: {ex.GetType().Name} - {ex.Message}");
                    UpdateServerStatus(status, false, "ComfyUI");
                }
            }
            catch (Exception ex)
            {
                // Outer catch for any unexpected errors
                RecordFailure("ComfyUI");
                System.Diagnostics.Debug.WriteLine($"ComfyUI health check outer error: {ex.GetType().Name} - {ex.Message}");
                UpdateServerStatus(status, false, "ComfyUI");
            }
        }

        private void UpdateServerStatus(ServerStatus status, bool isRunning, string serverName)
        {
            if (status.IsRunning == isRunning)
                return;

            var previousStatus = new ServerStatus
            {
                Name = status.Name,
                IsRunning = status.IsRunning,
                Endpoint = status.Endpoint,
                Uptime = status.Uptime,
                LastStarted = status.LastStarted,
                LastStopped = status.LastStopped,
                Type = status.Type,
                CpuUsage = status.CpuUsage,
                MemoryUsage = status.MemoryUsage
            };

            status.IsRunning = isRunning;
            if (isRunning && !previousStatus.IsRunning)
            {
                status.LastStarted = DateTime.Now;
            }
            else if (!isRunning && previousStatus.IsRunning)
            {
                status.LastStopped = DateTime.Now;
            }

            ServerStatusChanged?.Invoke(this, new ServerStatusChangedEventArgs
            {
                ServerName = serverName,
                PreviousStatus = previousStatus,
                CurrentStatus = status
            });
        }

        public void Dispose()
        {
            // Unsubscribe from event handlers
            if (_mcpService != null)
            {
                try
                {
                    _mcpService.ServerStatusChanged -= OnMCPServerStatusChanged;
                }
                catch { }
            }

            if (_virtualEnvironmentService != null)
            {
                try
                {
                    _virtualEnvironmentService.StatusChanged -= OnVirtualEnvironmentStatusChanged;
                }
                catch { }
            }

            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            
            if (_gpuCounters != null)
            {
                foreach (var counter in _gpuCounters)
                {
                    try
                    {
                        counter.Dispose();
                    }
                    catch { }
                }
                _gpuCounters.Clear();
                _gpuCounters = null;
            }
            
            // Dispose HttpClient
            try
            {
                _httpClient?.Dispose();
            }
            catch { }

            try
            {
                _localTtsHost?.Dispose();
            }
            catch { }

            try
            {
                _covasBridge?.Dispose();
            }
            catch { }
            
            // Shutdown NVML if it was initialized
            try
            {
                NvmlWrapper.Shutdown();
            }
            catch { }
            
            // Cancel any in-flight checks to avoid ObjectDisposedException on IO completion threads
            try { _cts.Cancel(); } catch { }
            try { _cts.Dispose(); } catch { }
            _cpuCounter = null;
            _memoryCounter = null;
        }
    }
}
