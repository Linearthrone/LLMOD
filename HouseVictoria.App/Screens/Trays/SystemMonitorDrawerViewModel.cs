using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Core.Utils;
using HouseVictoria.App;
using HouseVictoria.App.Screens.Windows;

namespace HouseVictoria.App.Screens.Trays
{
    public class SystemMonitorDrawerViewModel : ObservableObject, IDisposable
    {
        private readonly ISystemMonitorService _systemMonitorService;
        private readonly IVirtualEnvironmentService? _virtualEnvironmentService;
        private readonly AppConfig? _appConfig;
        private readonly System.Windows.Controls.Border _drawerPanel;
        private readonly Dispatcher _dispatcher;

        private bool _isDrawerOpen;
        public bool IsDrawerOpen
        {
            get => _isDrawerOpen;
            set
            {
                if (SetProperty(ref _isDrawerOpen, value))
                {
                    _drawerPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        // System Metrics
        private string _systemUptime = "00:00:00";
        public string SystemUptime
        {
            get => _systemUptime;
            set => SetProperty(ref _systemUptime, value);
        }

        private string _cpuUsage = "0%";
        public string CPUUsage
        {
            get => _cpuUsage;
            set => SetProperty(ref _cpuUsage, value);
        }

        private double _cpuUsageValue;
        public double CPUUsageValue
        {
            get => _cpuUsageValue;
            set => SetProperty(ref _cpuUsageValue, value);
        }

        private string _cpuTemperature = "0°C";
        public string CPUTemperature
        {
            get => _cpuTemperature;
            set => SetProperty(ref _cpuTemperature, value);
        }

        private string _cpuFanSpeed = "0 RPM";
        public string CPUFanSpeed
        {
            get => _cpuFanSpeed;
            set => SetProperty(ref _cpuFanSpeed, value);
        }

        private string _gpuUsage = "0%";
        public string GPUUsage
        {
            get => _gpuUsage;
            set => SetProperty(ref _gpuUsage, value);
        }

        private double _gpuUsageValue;
        public double GPUUsageValue
        {
            get => _gpuUsageValue;
            set => SetProperty(ref _gpuUsageValue, value);
        }

        private string _gpuTemperature = "0°C";
        public string GPUTemperature
        {
            get => _gpuTemperature;
            set => SetProperty(ref _gpuTemperature, value);
        }

        private string _gpuFanSpeed = "0 RPM";
        public string GPUFanSpeed
        {
            get => _gpuFanSpeed;
            set => SetProperty(ref _gpuFanSpeed, value);
        }

        private string _gpuVendor = "Unknown";
        public string GPUVendor
        {
            get => _gpuVendor;
            set => SetProperty(ref _gpuVendor, value);
        }

        private bool _gpuMetricsAvailable = false;
        public bool GPUMetricsAvailable
        {
            get => _gpuMetricsAvailable;
            set => SetProperty(ref _gpuMetricsAvailable, value);
        }

        private string _ramUsage = "0 MB / 0 MB";
        public string RAMUsage
        {
            get => _ramUsage;
            set => SetProperty(ref _ramUsage, value);
        }

        private double _ramUsagePercentage;
        public double RAMUsagePercentage
        {
            get => _ramUsagePercentage;
            set => SetProperty(ref _ramUsagePercentage, value);
        }

        // AI Status
        private string _primaryAIStatusText = "Inactive";
        public string PrimaryAIStatusText
        {
            get => _primaryAIStatusText;
            set => SetProperty(ref _primaryAIStatusText, value);
        }

        private Brush _primaryAIStatusColor = Brushes.Red;
        public Brush PrimaryAIStatusColor
        {
            get => _primaryAIStatusColor;
            set => SetProperty(ref _primaryAIStatusColor, value);
        }

        private string _currentAIStatusText = "No Contact";
        public string CurrentAIStatusText
        {
            get => _currentAIStatusText;
            set => SetProperty(ref _currentAIStatusText, value);
        }

        private Brush _currentAIStatusColor = Brushes.Gray;
        public Brush CurrentAIStatusColor
        {
            get => _currentAIStatusColor;
            set => SetProperty(ref _currentAIStatusColor, value);
        }

        // Virtual Environment Status
        private string _virtualEnvironmentStatusText = "Disconnected";
        public string VirtualEnvironmentStatusText
        {
            get => _virtualEnvironmentStatusText;
            set => SetProperty(ref _virtualEnvironmentStatusText, value);
        }

        private Brush _virtualEnvironmentStatusColor = Brushes.Red;
        public Brush VirtualEnvironmentStatusColor
        {
            get => _virtualEnvironmentStatusColor;
            set => SetProperty(ref _virtualEnvironmentStatusColor, value);
        }

        private string _virtualEnvironmentDetails = "Not connected";
        public string VirtualEnvironmentDetails
        {
            get => _virtualEnvironmentDetails;
            set => SetProperty(ref _virtualEnvironmentDetails, value);
        }

        private bool _virtualEnvironmentIsConnected = false;
        public bool VirtualEnvironmentIsConnected
        {
            get => _virtualEnvironmentIsConnected;
            set => SetProperty(ref _virtualEnvironmentIsConnected, value);
        }

        private string _virtualEnvironmentConnectButtonText = "Connect";
        public string VirtualEnvironmentConnectButtonText
        {
            get => _virtualEnvironmentConnectButtonText;
            set => SetProperty(ref _virtualEnvironmentConnectButtonText, value);
        }

        private string _virtualEnvironmentSceneName = "No scene";
        public string VirtualEnvironmentSceneName
        {
            get => _virtualEnvironmentSceneName;
            set => SetProperty(ref _virtualEnvironmentSceneName, value);
        }

        private string _virtualEnvironmentSceneDetails = "";
        public string VirtualEnvironmentSceneDetails
        {
            get => _virtualEnvironmentSceneDetails;
            set => SetProperty(ref _virtualEnvironmentSceneDetails, value);
        }

        // Server Statuses
        public ObservableCollection<ServerStatusViewModel> ServerStatuses { get; } = new();

        // Commands
        public ICommand ToggleDrawerCommand { get; }
        public ICommand ShutdownAllCommand { get; }
        public ICommand VirtualEnvironmentConnectCommand { get; }
        public ICommand VirtualEnvironmentDisconnectCommand { get; }
        public ICommand VirtualEnvironmentOpenControlsCommand { get; }

        public SystemMonitorDrawerViewModel(ISystemMonitorService systemMonitorService, System.Windows.Controls.Border drawerPanel)
        {
            _systemMonitorService = systemMonitorService ?? throw new ArgumentNullException(nameof(systemMonitorService));
            _drawerPanel = drawerPanel ?? throw new ArgumentNullException(nameof(drawerPanel));
            _dispatcher = drawerPanel.Dispatcher;
            
            // Try to get VirtualEnvironmentService and AppConfig from DI
            try
            {
                _virtualEnvironmentService = App.GetService<IVirtualEnvironmentService>();
                _appConfig = App.GetService<AppConfig>();
                
                if (_virtualEnvironmentService != null)
                {
                    _virtualEnvironmentService.StatusChanged += OnVirtualEnvironmentStatusChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not get VirtualEnvironmentService or AppConfig: {ex.Message}");
            }
            
            ToggleDrawerCommand = new RelayCommand(() => IsDrawerOpen = !IsDrawerOpen);
            ShutdownAllCommand = new RelayCommand(async () => await ShutdownAllServersAsync());
            VirtualEnvironmentConnectCommand = new RelayCommand(async () => await ConnectVirtualEnvironmentAsync());
            VirtualEnvironmentDisconnectCommand = new RelayCommand(async () => await DisconnectVirtualEnvironmentAsync());
            VirtualEnvironmentOpenControlsCommand = new RelayCommand(() => OpenVirtualEnvironmentControls());

            // Initialize drawer as closed
            _drawerPanel.Visibility = Visibility.Collapsed;

            // Load initial server statuses
            _ = LoadServerStatusesAsync();

            // Subscribe to server status changes
            _systemMonitorService.ServerStatusChanged += OnServerStatusChanged;
        }

        private void OnVirtualEnvironmentStatusChanged(object? sender, VirtualEnvironmentEventArgs e)
        {
            _dispatcher.InvokeAsync(() =>
            {
                VirtualEnvironmentIsConnected = e.Status.IsConnected;
                VirtualEnvironmentStatusText = e.Status.IsConnected ? "Connected" : "Disconnected";
                VirtualEnvironmentStatusColor = e.Status.IsConnected ? Brushes.Green : Brushes.Red;
                VirtualEnvironmentDetails = e.Status.IsConnected 
                    ? $"Avatars: {e.Status.AvatarCount}, FPS: {e.Status.FrameRate:F1}, Uptime: {FormatTimeSpan(e.Status.Uptime)}"
                    : "Not connected";
                VirtualEnvironmentConnectButtonText = e.Status.IsConnected ? "Reconnect" : "Connect";
                
                // Update scene information
                if (e.Status.IsConnected)
                {
                    VirtualEnvironmentSceneName = e.Status.CurrentScene ?? "Unknown Scene";
                    VirtualEnvironmentSceneDetails = $"Avatars: {e.Status.AvatarCount} | FPS: {e.Status.FrameRate:F1} | Rendering: {(e.Status.IsRendering ? "Yes" : "No")}";
                }
                else
                {
                    VirtualEnvironmentSceneName = "No scene";
                    VirtualEnvironmentSceneDetails = "";
                }
            });
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            else if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}m {ts.Seconds}s";
            else
                return $"{ts.Seconds}s";
        }

        private void OpenVirtualEnvironmentControls()
        {
            try
            {
                // Publish event to open Virtual Environment Controls window
                var eventAggregator = App.GetService<IEventAggregator>();
                eventAggregator?.Publish(new ShowWindowEvent { WindowType = "VirtualEnvironmentControls" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening Virtual Environment Controls: {ex.Message}");
                MessageBox.Show($"Error opening Virtual Environment Controls: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ConnectVirtualEnvironmentAsync()
        {
            if (_virtualEnvironmentService == null || _appConfig == null)
            {
                MessageBox.Show("Virtual Environment service is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var endpoint = _appConfig.UnrealEngineEndpoint;
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                MessageBox.Show("Please configure the Unreal Engine endpoint in Settings first.", "Configuration Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                VirtualEnvironmentConnectButtonText = "Connecting...";
                var connected = await _virtualEnvironmentService.ConnectAsync(endpoint);
                if (!connected)
                {
                    MessageBox.Show($"Failed to connect to Virtual Environment at {endpoint}. Please check that Unreal Engine is running and the WebSocket server is active.", "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to Virtual Environment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                VirtualEnvironmentConnectButtonText = VirtualEnvironmentIsConnected ? "Reconnect" : "Connect";
            }
        }

        private async Task DisconnectVirtualEnvironmentAsync()
        {
            if (_virtualEnvironmentService == null) return;

            try
            {
                await _virtualEnvironmentService.DisconnectAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error disconnecting from Virtual Environment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnServerStatusChanged(object? sender, ServerStatusChangedEventArgs e)
        {
            // Marshal to UI thread
            _ = _dispatcher.InvokeAsync(async () => await LoadServerStatusesAsync(), DispatcherPriority.Normal);
        }

        private async Task LoadServerStatusesAsync()
        {
            try
            {
                var statuses = await _systemMonitorService.GetAllServerStatusesAsync();
                
                // Ensure collection updates happen on UI thread
                await _dispatcher.InvokeAsync(() =>
                {
                    ServerStatuses.Clear();
                    foreach (var status in statuses.Values)
                    {
                        ServerStatuses.Add(new ServerStatusViewModel(status, _systemMonitorService));
                    }
                }, DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading server statuses: {ex.Message}");
            }
        }

        private async Task ShutdownAllServersAsync()
        {
            try
            {
                await _systemMonitorService.ShutdownAllServersAsync();
                await LoadServerStatusesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error shutting down servers: {ex.Message}");
            }
        }

        public void UpdateMetrics()
        {
            try
            {
                var metrics = _systemMonitorService.GetCurrentMetrics();
                
                // CPU
                CPUUsage = $"{metrics.CPUUsage:F1}%";
                CPUUsageValue = metrics.CPUUsage;
                CPUTemperature = $"{metrics.CPUTemperature:F1}°C";
                CPUFanSpeed = $"{metrics.CPUFanSpeed:F0} RPM";

                // GPU
                GPUUsage = $"{metrics.GPUUsage:F1}%";
                GPUUsageValue = metrics.GPUUsage;
                
                // Detect GPU vendor and availability
                GPUMetricsAvailable = metrics.GPUUsage > 0 || metrics.GPUTemperature > 0 || metrics.GPUFanSpeed > 0;
                
                // Try to detect vendor from system monitor service
                // For now, if metrics are available, assume NVIDIA (NVML) or set to Unknown
                if (GPUMetricsAvailable)
                {
                    // If we have valid GPU metrics, likely NVIDIA (NVML) or could be other vendor
                    // In a full implementation, we'd query the service for vendor info
                    GPUVendor = metrics.GPUTemperature > 0 ? "NVIDIA" : "Unknown";
                }
                else
                {
                    GPUVendor = "Not Supported";
                }
                
                if (GPUMetricsAvailable)
                {
                    GPUTemperature = $"{metrics.GPUTemperature:F1}°C";
                    GPUFanSpeed = $"{metrics.GPUFanSpeed:F0} RPM";
                }
                else
                {
                    GPUTemperature = "N/A";
                    GPUFanSpeed = "N/A";
                }

                // RAM
                RAMUsage = $"{metrics.RAMUsed} MB / {metrics.RAMTotal} MB";
                RAMUsagePercentage = metrics.RAMUsagePercentage;

                // System Uptime
                var uptime = _systemMonitorService.GetSystemUptime();
                SystemUptime = $"{uptime.Days}d {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

                // AI Status
                var primaryAI = _systemMonitorService.GetPrimaryAIStatus();
                PrimaryAIStatusText = primaryAI.IsActive ? $"Active - {primaryAI.CurrentTask}" : "Inactive";
                PrimaryAIStatusColor = primaryAI.IsActive ? Brushes.Green : Brushes.Red;

                var currentAI = _systemMonitorService.GetCurrentAIContactStatus();
                CurrentAIStatusText = currentAI.IsActive ? currentAI.Name : "No Contact";
                CurrentAIStatusColor = currentAI.IsActive ? Brushes.Green : Brushes.Gray;

                // Virtual Environment Status
                var veStatus = _systemMonitorService.GetVirtualEnvironmentStatus();
                VirtualEnvironmentIsConnected = veStatus.IsConnected;
                VirtualEnvironmentStatusText = veStatus.IsConnected ? "Connected" : "Disconnected";
                VirtualEnvironmentStatusColor = veStatus.IsConnected ? Brushes.Green : Brushes.Red;
                VirtualEnvironmentDetails = veStatus.IsConnected 
                    ? $"Avatars: {veStatus.AvatarCount}, FPS: {veStatus.FrameRate:F1}, Uptime: {FormatTimeSpan(veStatus.Uptime)}"
                    : "Not connected";
                VirtualEnvironmentConnectButtonText = veStatus.IsConnected ? "Reconnect" : "Connect";
                
                // Update scene information
                if (veStatus.IsConnected)
                {
                    VirtualEnvironmentSceneName = veStatus.CurrentScene ?? "Unknown Scene";
                    VirtualEnvironmentSceneDetails = $"Avatars: {veStatus.AvatarCount} | FPS: {veStatus.FrameRate:F1} | Rendering: {(veStatus.IsRendering ? "Yes" : "No")}";
                }
                else
                {
                    VirtualEnvironmentSceneName = "No scene";
                    VirtualEnvironmentSceneDetails = "";
                }

                // Update server statuses periodically
                _ = LoadServerStatusesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating metrics: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_systemMonitorService is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public class ServerStatusViewModel : ObservableObject
    {
        private readonly ServerStatus _status;
        private readonly ISystemMonitorService _systemMonitorService;

        public string Name => _status.Name;
        public bool IsRunning => _status.IsRunning;
        public Brush StatusColor => IsRunning ? Brushes.Green : Brushes.Red;
        public ICommand RestartCommand { get; }
        public ICommand StopCommand { get; }

        public ServerStatusViewModel(ServerStatus status, ISystemMonitorService systemMonitorService)
        {
            _status = status;
            _systemMonitorService = systemMonitorService;
            RestartCommand = new RelayCommand(async () => await RestartServerAsync());
            StopCommand = new RelayCommand(async () => await StopServerAsync());
        }

        private async Task RestartServerAsync()
        {
            try
            {
                await _systemMonitorService.RestartServerAsync(_status.Name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restarting server {_status.Name}: {ex.Message}");
            }
        }

        private async Task StopServerAsync()
        {
            try
            {
                await _systemMonitorService.StopServerAsync(_status.Name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping server {_status.Name}: {ex.Message}");
            }
        }
    }
}
