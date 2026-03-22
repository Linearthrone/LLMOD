using HouseVictoria.Core.Models;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using System.Configuration;
using System.Windows;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Linq;
using Microsoft.Win32;
using HouseVictoria.App.Services;
using HouseVictoria.Services.AIServices;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;

namespace HouseVictoria.App.Screens.Windows
{
    public class SettingsWindowViewModel : ObservableObject
    {
        private readonly AppConfig _appConfig;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
        private readonly ITTSService? _ttsService;
        private string? _validationError;
        private bool _isTestingConnection;
        private string? _connectionTestResult;
        
        // LLM Server Settings
        private string _lmStudioEndpoint = string.Empty;
        public string LmStudioEndpoint
        {
            get => _lmStudioEndpoint;
            set
            {
                if (SetProperty(ref _lmStudioEndpoint, value))
                    ValidateSettings();
            }
        }

        private string _primaryLLM = "ollama";
        /// <summary>Primary LLM: "ollama", "lmstudio", or "anythingllm". Only one can be primary.</summary>
        public string PrimaryLLM
        {
            get => _primaryLLM;
            set
            {
                var normalized = (value ?? "ollama").ToLowerInvariant();
                if (normalized != "ollama" && normalized != "lmstudio" && normalized != "anythingllm")
                    normalized = "ollama";
                if (SetProperty(ref _primaryLLM, normalized))
                {
                    OnPropertyChanged(nameof(UseLmStudioAsPrimary));
                    OnPropertyChanged(nameof(UseOllamaAsPrimary));
                    OnPropertyChanged(nameof(UseAnythingLLMAsPrimary));
                }
            }
        }

        public bool UseLmStudioAsPrimary
        {
            get => string.Equals(_primaryLLM, "lmstudio", StringComparison.OrdinalIgnoreCase);
            set { if (value) SetPrimaryLLM("lmstudio"); else if (UseLmStudioAsPrimary) SetPrimaryLLM("ollama"); }
        }

        public bool UseOllamaAsPrimary
        {
            get => string.Equals(_primaryLLM, "ollama", StringComparison.OrdinalIgnoreCase);
            set { if (value) SetPrimaryLLM("ollama"); else if (UseOllamaAsPrimary) SetPrimaryLLM("lmstudio"); }
        }

        public bool UseAnythingLLMAsPrimary
        {
            get => string.Equals(_primaryLLM, "anythingllm", StringComparison.OrdinalIgnoreCase);
            set { if (value) SetPrimaryLLM("anythingllm"); else if (UseAnythingLLMAsPrimary) SetPrimaryLLM("ollama"); }
        }

        private void SetPrimaryLLM(string value)
        {
            var normalized = value.ToLowerInvariant();
            if (_primaryLLM == normalized) return;
            _primaryLLM = normalized;
            OnPropertyChanged(nameof(PrimaryLLM));
            OnPropertyChanged(nameof(UseLmStudioAsPrimary));
            OnPropertyChanged(nameof(UseOllamaAsPrimary));
            OnPropertyChanged(nameof(UseAnythingLLMAsPrimary));
        }

        private string? _lmStudioConnectionStatus;
        public string? LmStudioConnectionStatus
        {
            get => _lmStudioConnectionStatus;
            set => SetProperty(ref _lmStudioConnectionStatus, value);
        }

        private string _ollamaEndpoint = string.Empty;
        public string OllamaEndpoint
        {
            get => _ollamaEndpoint;
            set 
            { 
                if (SetProperty(ref _ollamaEndpoint, value))
                {
                    ValidateSettings();
                }
            }
        }

        private string _anythingLLMEndpoint = string.Empty;
        public string AnythingLLMEndpoint
        {
            get => _anythingLLMEndpoint;
            set
            {
                if (SetProperty(ref _anythingLLMEndpoint, value))
                    ValidateSettings();
            }
        }

        private string? _anythingLLMConnectionStatus;
        public string? AnythingLLMConnectionStatus
        {
            get => _anythingLLMConnectionStatus;
            set => SetProperty(ref _anythingLLMConnectionStatus, value);
        }

        // MCP Server Settings
        private string _mcpServerEndpoint = string.Empty;
        public string MCPServerEndpoint
        {
            get => _mcpServerEndpoint;
            set 
            { 
                if (SetProperty(ref _mcpServerEndpoint, value))
                {
                    ValidateSettings();
                }
            }
        }

        // TTS Settings
        private string _ttsEndpoint = string.Empty;
        public string TTSEndpoint
        {
            get => _ttsEndpoint;
            set 
            { 
                if (SetProperty(ref _ttsEndpoint, value))
                {
                    ValidateSettings();
                }
            }
        }

        // STT (Speech-to-Text) Settings
        private string _sttEndpoint = string.Empty;
        public string STTEndpoint
        {
            get => _sttEndpoint;
            set { if (SetProperty(ref _sttEndpoint, value ?? string.Empty)) ValidateSettings(); }
        }

        // Virtual Environment Settings
        private string _unrealEngineEndpoint = string.Empty;
        public string UnrealEngineEndpoint
        {
            get => _unrealEngineEndpoint;
            set 
            { 
                if (SetProperty(ref _unrealEngineEndpoint, value))
                {
                    ValidateSettings();
                }
            }
        }

        // Image Generation Endpoint (ComfyUI - legacy StableDiffusionEndpoint setting name)
        private string _stableDiffusionEndpoint = string.Empty;
        public string StableDiffusionEndpoint
        {
            get => _stableDiffusionEndpoint;
            set 
            { 
                if (SetProperty(ref _stableDiffusionEndpoint, value))
                {
                    ValidateSettings();
                }
            }
        }

        // Stability Matrix & ComfyUI (image/video generation)
        private string _stabilityMatrixPath = string.Empty;
        public string StabilityMatrixPath
        {
            get => _stabilityMatrixPath;
            set => SetProperty(ref _stabilityMatrixPath, value ?? string.Empty);
        }

        private string _comfyUIPortablePath = string.Empty;
        public string ComfyUIPortablePath
        {
            get => _comfyUIPortablePath;
            set => SetProperty(ref _comfyUIPortablePath, value ?? string.Empty);
        }

        // Color Scheme
        private int _selectedThemeIndex;
        public int SelectedThemeIndex
        {
            get => _selectedThemeIndex;
            set => SetProperty(ref _selectedThemeIndex, value);
        }

        public string SelectedThemeId => ThemeManager.GetThemeIdByIndex(SelectedThemeIndex);

        public ObservableCollection<ThemeInfo> AvailableThemes { get; } = new();

        // Overlay Settings
        private bool _enableOverlay;
        public bool EnableOverlay
        {
            get => _enableOverlay;
            set => SetProperty(ref _enableOverlay, value);
        }

        private double _overlayOpacity;
        public double OverlayOpacity
        {
            get => _overlayOpacity;
            set 
            { 
                if (SetProperty(ref _overlayOpacity, value))
                {
                    ValidateSettings();
                }
            }
        }

        private bool _autoHideTrays;
        public bool AutoHideTrays
        {
            get => _autoHideTrays;
            set => SetProperty(ref _autoHideTrays, value);
        }

        private int _autoHideDelayMs;
        public int AutoHideDelayMs
        {
            get => _autoHideDelayMs;
            set 
            { 
                if (SetProperty(ref _autoHideDelayMs, value))
                {
                    ValidateSettings();
                }
            }
        }

        // Locomotion Settings
        private double _walkSpeed = 1.0;
        public double WalkSpeed
        {
            get => _walkSpeed;
            set 
            { 
                if (SetProperty(ref _walkSpeed, value))
                {
                    ValidateSettings();
                }
            }
        }

        private double _runSpeed = 2.0;
        public double RunSpeed
        {
            get => _runSpeed;
            set 
            { 
                if (SetProperty(ref _runSpeed, value))
                {
                    ValidateSettings();
                }
            }
        }

        private double _jumpHeight = 1.0;
        public double JumpHeight
        {
            get => _jumpHeight;
            set 
            { 
                if (SetProperty(ref _jumpHeight, value))
                {
                    ValidateSettings();
                }
            }
        }

        private bool _enablePhysicsInteraction = true;
        public bool EnablePhysicsInteraction
        {
            get => _enablePhysicsInteraction;
            set => SetProperty(ref _enablePhysicsInteraction, value);
        }

        // Tools Configuration
        private bool _enableFileSystemAccess = true;
        public bool EnableFileSystemAccess
        {
            get => _enableFileSystemAccess;
            set => SetProperty(ref _enableFileSystemAccess, value);
        }

        private bool _enableNetworkAccess = true;
        public bool EnableNetworkAccess
        {
            get => _enableNetworkAccess;
            set => SetProperty(ref _enableNetworkAccess, value);
        }

        private bool _enableSystemCommands = false;
        public bool EnableSystemCommands
        {
            get => _enableSystemCommands;
            set => SetProperty(ref _enableSystemCommands, value);
        }

        // Persistent Memory Configuration
        private bool _enablePersistentMemory = true;
        public bool EnablePersistentMemory
        {
            get => _enablePersistentMemory;
            set => SetProperty(ref _enablePersistentMemory, value);
        }

        private string _persistentMemoryPath = "Data/Memory";
        public string PersistentMemoryPath
        {
            get => _persistentMemoryPath;
            set => SetProperty(ref _persistentMemoryPath, value);
        }

        private int _memoryMaxEntries = 10000;
        public int MemoryMaxEntries
        {
            get => _memoryMaxEntries;
            set 
            { 
                if (SetProperty(ref _memoryMaxEntries, value))
                {
                    ValidateSettings();
                }
            }
        }

        private double _memoryImportanceThreshold = 0.5;
        public double MemoryImportanceThreshold
        {
            get => _memoryImportanceThreshold;
            set 
            { 
                if (SetProperty(ref _memoryImportanceThreshold, value))
                {
                    ValidateSettings();
                }
            }
        }

        private int _memoryRetentionDays = 90;
        public int MemoryRetentionDays
        {
            get => _memoryRetentionDays;
            set 
            { 
                if (SetProperty(ref _memoryRetentionDays, value))
                {
                    ValidateSettings();
                }
            }
        }

        // Validation and Connection Testing
        public string? ValidationError
        {
            get => _validationError;
            set => SetProperty(ref _validationError, value);
        }

        public bool IsTestingConnection
        {
            get => _isTestingConnection;
            set => SetProperty(ref _isTestingConnection, value);
        }

        public string? ConnectionTestResult
        {
            get => _connectionTestResult;
            set => SetProperty(ref _connectionTestResult, value);
        }

        // Individual connection status indicators
        private string? _ollamaConnectionStatus;
        public string? OllamaConnectionStatus
        {
            get => _ollamaConnectionStatus;
            set => SetProperty(ref _ollamaConnectionStatus, value);
        }

        private string? _mcpConnectionStatus;
        public string? MCPServerConnectionStatus
        {
            get => _mcpConnectionStatus;
            set => SetProperty(ref _mcpConnectionStatus, value);
        }

        private string? _ttsConnectionStatus;
        public string? TTSConnectionStatus
        {
            get => _ttsConnectionStatus;
            set => SetProperty(ref _ttsConnectionStatus, value);
        }

        private string? _sttConnectionStatus;
        public string? STTConnectionStatus
        {
            get => _sttConnectionStatus;
            set => SetProperty(ref _sttConnectionStatus, value);
        }

        private string _kokoroTTSStatus = "—";
        public string KokoroTTSStatus
        {
            get => _kokoroTTSStatus;
            set => SetProperty(ref _kokoroTTSStatus, value);
        }

        private string? _unrealConnectionStatus;
        public string? UnrealConnectionStatus
        {
            get => _unrealConnectionStatus;
            set => SetProperty(ref _unrealConnectionStatus, value);
        }

        private string? _stableDiffusionConnectionStatus;
        public string? StableDiffusionConnectionStatus
        {
            get => _stableDiffusionConnectionStatus;
            set => SetProperty(ref _stableDiffusionConnectionStatus, value);
        }

        public bool IsValid => string.IsNullOrWhiteSpace(_validationError);

        // Commands
        public ICommand TestOllamaConnectionCommand { get; }
        public ICommand TestLmStudioConnectionCommand { get; }
        public ICommand TestAnythingLLMConnectionCommand { get; }
        public ICommand TestMCPConnectionCommand { get; }
        public ICommand TestTTSConnectionCommand { get; }
        public ICommand TestSTTConnectionCommand { get; }
        public ICommand TestUnrealConnectionCommand { get; }
        public ICommand TestStableDiffusionConnectionCommand { get; }
        public ICommand StartStabilityMatrixCommand { get; }
        public ICommand StartComfyUICommand { get; }
        public ICommand BrowseStabilityMatrixPathCommand { get; }
        public ICommand BrowseComfyUIPortablePathCommand { get; }
        public ICommand ImportSettingsCommand { get; }
        public ICommand ExportSettingsCommand { get; }
        public ICommand ResetToDefaultsCommand { get; }
        public ICommand StartKokoroCommand { get; }
        public ICommand StopKokoroCommand { get; }

        public SettingsWindowViewModel(AppConfig appConfig)
        {
            _appConfig = appConfig;
            
            // Get TTS service from service provider
            try
            {
                _ttsService = App.ServiceProvider?.GetService<ITTSService>();
            }
            catch
            {
                _ttsService = null;
            }
            
            // Load settings from AppConfig
            LmStudioEndpoint = appConfig.LmStudioEndpoint;
            PrimaryLLM = appConfig.PrimaryLLM;
            OllamaEndpoint = appConfig.OllamaEndpoint;
            AnythingLLMEndpoint = appConfig.AnythingLLMEndpoint;
            MCPServerEndpoint = appConfig.MCPServerEndpoint;
            TTSEndpoint = appConfig.TTSEndpoint;
            STTEndpoint = appConfig.STTEndpoint ?? string.Empty;
            UnrealEngineEndpoint = appConfig.UnrealEngineEndpoint;
            StableDiffusionEndpoint = appConfig.StableDiffusionEndpoint;
            StabilityMatrixPath = appConfig.StabilityMatrixPath ?? string.Empty;
            ComfyUIPortablePath = appConfig.ComfyUIPortablePath ?? string.Empty;
            SelectedThemeIndex = ThemeManager.GetThemeIndexById(appConfig.ColorScheme ?? "CyanBlueDark");
            foreach (var t in ThemeManager.Themes)
                AvailableThemes.Add(t);
            EnableOverlay = appConfig.EnableOverlay;
            OverlayOpacity = appConfig.OverlayOpacity;
            AutoHideTrays = appConfig.AutoHideTrays;
            AutoHideDelayMs = appConfig.AutoHideDelayMs;

            // Load advanced settings
            WalkSpeed = appConfig.WalkSpeed;
            RunSpeed = appConfig.RunSpeed;
            JumpHeight = appConfig.JumpHeight;
            EnablePhysicsInteraction = appConfig.EnablePhysicsInteraction;
            EnableFileSystemAccess = appConfig.EnableFileSystemAccess;
            EnableNetworkAccess = appConfig.EnableNetworkAccess;
            EnableSystemCommands = appConfig.EnableSystemCommands;
            EnablePersistentMemory = appConfig.EnablePersistentMemory;
            PersistentMemoryPath = appConfig.PersistentMemoryPath;
            MemoryMaxEntries = appConfig.MemoryMaxEntries;
            MemoryImportanceThreshold = appConfig.MemoryImportanceThreshold;
            MemoryRetentionDays = appConfig.MemoryRetentionDays;

            // Initialize commands
            TestOllamaConnectionCommand = new RelayCommand(async () => await TestOllamaConnectionAsync());
            TestLmStudioConnectionCommand = new RelayCommand(async () => await TestLmStudioConnectionAsync());
            TestAnythingLLMConnectionCommand = new RelayCommand(async () => await TestAnythingLLMConnectionAsync());
            TestMCPConnectionCommand = new RelayCommand(async () => await TestMCPConnectionAsync());
            TestTTSConnectionCommand = new RelayCommand(async () => await TestTTSConnectionAsync());
            TestSTTConnectionCommand = new RelayCommand(async () => await TestSTTConnectionAsync());
            TestUnrealConnectionCommand = new RelayCommand(async () => await TestUnrealConnectionAsync());
            TestStableDiffusionConnectionCommand = new RelayCommand(async () => await TestStableDiffusionConnectionAsync());
            StartStabilityMatrixCommand = new RelayCommand(() => StartStabilityMatrix());
            StartComfyUICommand = new RelayCommand(() => StartComfyUI());
            BrowseStabilityMatrixPathCommand = new RelayCommand(() => BrowseStabilityMatrixPath());
            BrowseComfyUIPortablePathCommand = new RelayCommand(() => BrowseComfyUIPortablePath());
            ImportSettingsCommand = new RelayCommand(() => ImportSettings());
            ExportSettingsCommand = new RelayCommand(() => ExportSettings());
            ResetToDefaultsCommand = new RelayCommand(() => ResetToDefaults());
            StartKokoroCommand = new RelayCommand(async () => await StartKokoroAsync());
            StopKokoroCommand = new RelayCommand(async () => await StopKokoroAsync());

            ValidateSettings();
            
            _ = RefreshKokoroStatusAsync();
        }

        private void ValidateSettings()
        {
            _validationError = null;

            // Validate URL format
            var urlPattern = @"^https?://.+|^ws://.+|^wss://.+";
            if (!string.IsNullOrWhiteSpace(LmStudioEndpoint) && !Regex.IsMatch(LmStudioEndpoint, urlPattern))
            {
                _validationError = "LM Studio endpoint must be a valid URL (http://, https://)";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (!string.IsNullOrWhiteSpace(OllamaEndpoint) && !Regex.IsMatch(OllamaEndpoint, urlPattern))
            {
                _validationError = "Ollama endpoint must be a valid URL (http://, https://)";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (!string.IsNullOrWhiteSpace(AnythingLLMEndpoint) && !Regex.IsMatch(AnythingLLMEndpoint, urlPattern))
            {
                _validationError = "Anything LLM endpoint must be a valid URL (http://, https://)";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (!string.IsNullOrWhiteSpace(MCPServerEndpoint) && !Regex.IsMatch(MCPServerEndpoint, urlPattern))
            {
                _validationError = "MCP Server endpoint must be a valid URL (http://, https://)";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (!string.IsNullOrWhiteSpace(TTSEndpoint) && !Regex.IsMatch(TTSEndpoint, urlPattern))
            {
                _validationError = "TTS endpoint must be a valid URL (http://, https://)";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (!string.IsNullOrWhiteSpace(STTEndpoint) && !Regex.IsMatch(STTEndpoint, urlPattern))
            {
                _validationError = "STT endpoint must be a valid URL (http://, https://)";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (!string.IsNullOrWhiteSpace(UnrealEngineEndpoint) && !Regex.IsMatch(UnrealEngineEndpoint, @"^ws://.+|^wss://.+"))
            {
                _validationError = "Unreal Engine endpoint must be a valid WebSocket URL (ws://, wss://)";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (!string.IsNullOrWhiteSpace(StableDiffusionEndpoint) && !Regex.IsMatch(StableDiffusionEndpoint, urlPattern))
            {
                _validationError = "Image generation endpoint must be a valid URL (http://, https://)";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            // Validate numeric ranges
            if (OverlayOpacity < 0.1 || OverlayOpacity > 1.0)
            {
                _validationError = "Overlay opacity must be between 0.1 and 1.0";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (AutoHideDelayMs < 0 || AutoHideDelayMs > 60000)
            {
                _validationError = "Auto-hide delay must be between 0 and 60000 milliseconds";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (WalkSpeed < 0.1 || WalkSpeed > 10.0)
            {
                _validationError = "Walk speed must be between 0.1 and 10.0";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (RunSpeed < 0.1 || RunSpeed > 20.0)
            {
                _validationError = "Run speed must be between 0.1 and 20.0";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (JumpHeight < 0.1 || JumpHeight > 10.0)
            {
                _validationError = "Jump height must be between 0.1 and 10.0";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (MemoryMaxEntries < 1 || MemoryMaxEntries > 1000000)
            {
                _validationError = "Memory max entries must be between 1 and 1000000";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (MemoryImportanceThreshold < 0.0 || MemoryImportanceThreshold > 1.0)
            {
                _validationError = "Memory importance threshold must be between 0.0 and 1.0";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            if (MemoryRetentionDays < 1 || MemoryRetentionDays > 3650)
            {
                _validationError = "Memory retention days must be between 1 and 3650";
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            OnPropertyChanged(nameof(ValidationError));
            OnPropertyChanged(nameof(IsValid));
        }

        private async Task TestOllamaConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(OllamaEndpoint))
            {
                OllamaConnectionStatus = "Error: Endpoint is empty";
                ConnectionTestResult = "Ollama: Error - Endpoint is empty";
                return;
            }

            IsTestingConnection = true;
            OllamaConnectionStatus = "Testing...";
            ConnectionTestResult = "Ollama: Testing connection...";

            try
            {
                var aiService = App.GetService<IAIService>();
                if (aiService != null)
                {
                    var result = await aiService.TestConnectionAsync(OllamaEndpoint);
                    OllamaConnectionStatus = result ? "✓ Connected" : "✗ Failed";
                    ConnectionTestResult = result ? "Ollama: ✓ Connection successful!" : "Ollama: ✗ Connection failed";
                }
                else
                {
                    var response = await _httpClient.GetAsync($"{OllamaEndpoint}/api/tags");
                    var success = response.IsSuccessStatusCode;
                    OllamaConnectionStatus = success ? "✓ Connected" : "✗ Failed";
                    ConnectionTestResult = success ? "Ollama: ✓ Connection successful!" : "Ollama: ✗ Connection failed";
                }
            }
            catch (Exception ex)
            {
                OllamaConnectionStatus = "✗ Failed";
                ConnectionTestResult = $"Ollama: ✗ Connection failed: {ex.Message}";
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        private async Task TestLmStudioConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(LmStudioEndpoint))
            {
                LmStudioConnectionStatus = "Error: Endpoint is empty";
                ConnectionTestResult = "LM Studio: Error - Endpoint is empty";
                return;
            }

            IsTestingConnection = true;
            LmStudioConnectionStatus = "Testing...";
            ConnectionTestResult = "LM Studio: Testing connection...";

            try
            {
                var aiService = App.GetService<IAIService>();
                if (aiService != null)
                {
                    var result = await aiService.TestConnectionAsync(LmStudioEndpoint);
                    LmStudioConnectionStatus = result ? "✓ Connected" : "✗ Failed";
                    ConnectionTestResult = result ? "LM Studio: ✓ Connection successful!" : "LM Studio: ✗ Connection failed";
                }
                else
                {
                    var response = await _httpClient.GetAsync($"{LmStudioEndpoint.TrimEnd('/')}/models");
                    var success = response.IsSuccessStatusCode;
                    LmStudioConnectionStatus = success ? "✓ Connected" : "✗ Failed";
                    ConnectionTestResult = success ? "LM Studio: ✓ Connection successful!" : "LM Studio: ✗ Connection failed";
                }
            }
            catch (Exception ex)
            {
                LmStudioConnectionStatus = "✗ Failed";
                ConnectionTestResult = $"LM Studio: ✗ Connection failed: {ex.Message}";
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        private async Task TestAnythingLLMConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(AnythingLLMEndpoint))
            {
                AnythingLLMConnectionStatus = "Error: Endpoint is empty";
                ConnectionTestResult = "Anything LLM: Error - Endpoint is empty";
                return;
            }

            IsTestingConnection = true;
            AnythingLLMConnectionStatus = "Testing...";
            ConnectionTestResult = "Anything LLM: Testing connection...";

            try
            {
                var baseUrl = AnythingLLMEndpoint.TrimEnd('/');
                var response = await _httpClient.GetAsync($"{baseUrl}/models");
                var success = response.IsSuccessStatusCode;
                AnythingLLMConnectionStatus = success ? "✓ Connected" : "✗ Failed";
                ConnectionTestResult = success ? "Anything LLM: ✓ Connection successful!" : "Anything LLM: ✗ Connection failed";
            }
            catch (Exception ex)
            {
                AnythingLLMConnectionStatus = "✗ Failed";
                ConnectionTestResult = $"Anything LLM: ✗ Connection failed: {ex.Message}";
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        private async Task TestMCPConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(MCPServerEndpoint))
            {
                MCPServerConnectionStatus = "Error: Endpoint is empty";
                ConnectionTestResult = "MCP Server: Error - Endpoint is empty";
                return;
            }

            IsTestingConnection = true;
            MCPServerConnectionStatus = "Testing...";
            ConnectionTestResult = "MCP Server: Testing connection...";

            try
            {
                var mcpService = App.GetService<IMCPService>();
                if (mcpService != null)
                {
                    var result = await mcpService.IsServerAvailableAsync(MCPServerEndpoint);
                    MCPServerConnectionStatus = result ? "✓ Connected" : "✗ Failed";
                    ConnectionTestResult = result ? "MCP Server: ✓ Connection successful!" : "MCP Server: ✗ Connection failed";
                }
                else
                {
                    // Fallback: direct HTTP test
                    var response = await _httpClient.GetAsync($"{MCPServerEndpoint}/health");
                    var success = response.IsSuccessStatusCode;
                    MCPServerConnectionStatus = success ? "✓ Connected" : "✗ Failed";
                    ConnectionTestResult = success ? "MCP Server: ✓ Connection successful!" : "MCP Server: ✗ Connection failed";
                }
            }
            catch (Exception ex)
            {
                MCPServerConnectionStatus = "✗ Failed";
                ConnectionTestResult = $"MCP Server: ✗ Connection failed: {ex.Message}";
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        private async Task TestTTSConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(TTSEndpoint))
            {
                TTSConnectionStatus = "Error: Endpoint is empty";
                ConnectionTestResult = "TTS: Error - Endpoint is empty";
                return;
            }

            IsTestingConnection = true;
            TTSConnectionStatus = "Testing...";
            ConnectionTestResult = "TTS: Testing connection...";

            try
            {
                // Try /health endpoint first, then root
                var endpoints = new[] { "/health", "/" };
                bool isConnected = false;

                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync($"{TTSEndpoint}{endpoint}");
                        if (response.IsSuccessStatusCode)
                        {
                            isConnected = true;
                            break;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                TTSConnectionStatus = isConnected ? "✓ Connected" : "✗ Failed";
                ConnectionTestResult = isConnected ? "TTS: ✓ Connection successful!" : "TTS: ✗ Connection failed";
            }
            catch (Exception ex)
            {
                TTSConnectionStatus = "✗ Failed";
                ConnectionTestResult = $"TTS: ✗ Connection failed: {ex.Message}";
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        private async Task TestSTTConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(STTEndpoint))
            {
                STTConnectionStatus = "Error: Endpoint is empty";
                ConnectionTestResult = "STT: Error - Endpoint is empty. Use e.g. http://localhost:8000/transcribe";
                return;
            }

            IsTestingConnection = true;
            STTConnectionStatus = "Testing...";
            ConnectionTestResult = "STT: Testing connection...";

            try
            {
                var baseUrl = STTEndpoint.TrimEnd('/');
                if (baseUrl.EndsWith("/transcribe", StringComparison.OrdinalIgnoreCase))
                    baseUrl = baseUrl[..baseUrl.LastIndexOf("/transcribe", StringComparison.OrdinalIgnoreCase)].TrimEnd('/');
                var healthUrl = $"{baseUrl}/health";

                var response = await _httpClient.GetAsync(healthUrl);
                if (response.IsSuccessStatusCode)
                {
                    STTConnectionStatus = "✓ Connected";
                    ConnectionTestResult = "STT: ✓ Connection successful! Speech-to-text is available.";
                }
                else
                {
                    STTConnectionStatus = "✗ Failed";
                    ConnectionTestResult = $"STT: ✗ Server returned {response.StatusCode}. Run start.bat to start the STT server, or set STT endpoint in Settings.";
                }
            }
            catch (Exception ex)
            {
                STTConnectionStatus = "✗ Failed";
                ConnectionTestResult = $"STT: ✗ Connection failed: {ex.Message}. Ensure the STT server is running (e.g. run start.bat) or configure STT endpoint.";
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        private async Task RefreshKokoroStatusAsync()
        {
            try
            {
                var monitor = App.ServiceProvider?.GetService<ISystemMonitorService>();
                if (monitor == null)
                {
                    KokoroTTSStatus = "—";
                    return;
                }
                var status = await monitor.GetServerStatusAsync("Kokoro TTS");
                KokoroTTSStatus = status.IsRunning ? "● Running (port 8880)" : "○ Stopped";
            }
            catch
            {
                KokoroTTSStatus = "—";
            }
        }

        private async Task StartKokoroAsync()
        {
            try
            {
                var monitor = App.ServiceProvider?.GetService<ISystemMonitorService>();
                if (monitor != null)
                {
                    await monitor.StartServerAsync("Kokoro TTS");
                    await Task.Delay(800);
                    await RefreshKokoroStatusAsync();
                    if (TTSEndpoint?.Contains("8880") == true)
                        _ = TestTTSConnectionAsync();
                }
            }
            catch (Exception ex)
            {
                KokoroTTSStatus = "Error: " + ex.Message;
            }
        }

        private async Task StopKokoroAsync()
        {
            try
            {
                var monitor = App.ServiceProvider?.GetService<ISystemMonitorService>();
                if (monitor != null)
                {
                    await monitor.StopServerAsync("Kokoro TTS");
                    await Task.Delay(500);
                    await RefreshKokoroStatusAsync();
                    if (TTSEndpoint?.Contains("8880") == true)
                        TTSConnectionStatus = "✗ Failed";
                }
            }
            catch (Exception ex)
            {
                KokoroTTSStatus = "Error: " + ex.Message;
            }
        }

        private async Task TestUnrealConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(UnrealEngineEndpoint))
            {
                UnrealConnectionStatus = "Error: Endpoint is empty";
                ConnectionTestResult = "Unreal Engine: Error - Endpoint is empty";
                return;
            }

            IsTestingConnection = true;
            UnrealConnectionStatus = "Testing...";
            ConnectionTestResult = "Unreal Engine: Testing WebSocket connection...";

            try
            {
                var virtualEnvService = App.GetService<IVirtualEnvironmentService>();
                if (virtualEnvService != null)
                {
                    var result = await virtualEnvService.ConnectAsync(UnrealEngineEndpoint);
                    UnrealConnectionStatus = result ? "✓ Connected" : "✗ Failed";
                    ConnectionTestResult = result ? "Unreal Engine: ✓ Connection successful!" : "Unreal Engine: ✗ Connection failed";
                    if (result)
                    {
                        await virtualEnvService.DisconnectAsync();
                    }
                }
                else
                {
                    // Fallback: direct WebSocket test
                    using var ws = new ClientWebSocket();
                    var uri = new Uri(UnrealEngineEndpoint);
                    await ws.ConnectAsync(uri, CancellationToken.None);
                    var success = ws.State == WebSocketState.Open;
                    UnrealConnectionStatus = success ? "✓ Connected" : "✗ Failed";
                    ConnectionTestResult = success ? "Unreal Engine: ✓ Connection successful!" : "Unreal Engine: ✗ Connection failed";
                    if (ws.State == WebSocketState.Open)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                UnrealConnectionStatus = "✗ Failed";
                ConnectionTestResult = $"Unreal Engine: ✗ Connection failed: {ex.Message}";
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        private async Task TestStableDiffusionConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(StableDiffusionEndpoint))
            {
                StableDiffusionConnectionStatus = "Error: Endpoint is empty";
                ConnectionTestResult = "Image endpoint: Error - Endpoint is empty";
                return;
            }

            IsTestingConnection = true;
            StableDiffusionConnectionStatus = "Testing...";
            ConnectionTestResult = "Image endpoint: Testing connection...";

            try
            {
                // For generation we call the Automatic1111-compatible API:
                // POST /sdapi/v1/txt2img and expect JSON with "images": ["<base64>", ...]
                // So the connection test should validate that the server actually exposes the sdapi surface.
                var optionsUrl = $"{StableDiffusionEndpoint}/sdapi/v1/options";
                using var response = await _httpClient.GetAsync(optionsUrl);

                if (response.IsSuccessStatusCode)
                {
                    StableDiffusionConnectionStatus = "✓ Connected";
                    ConnectionTestResult = "Image endpoint: ✓ Connection successful (Automatic1111 API detected).";
                }
                else
                {
                    // ComfyUI (typical :8188) does not implement /sdapi — it uses /prompt; treat ComfyUI as connected if checkpoints are listable.
                    try
                    {
                        var comfyBase = StableDiffusionEndpoint.TrimEnd('/');
                        using var ck = await _httpClient.GetAsync($"{comfyBase}/object_info/CheckpointLoaderSimple");
                        if (ck.IsSuccessStatusCode)
                        {
                            StableDiffusionConnectionStatus = "✓ Connected";
                            ConnectionTestResult = "Image endpoint: ✓ ComfyUI detected (generation uses native /prompt API).";
                            return;
                        }
                    }
                    catch
                    {
                        // fall through
                    }

                    StableDiffusionConnectionStatus = "✗ Failed";
                    ConnectionTestResult =
                        $"Image endpoint: ✗ Not Automatic1111 (GET /sdapi/v1/options → {(int)response.StatusCode}) and not ComfyUI (GET /object_info/CheckpointLoaderSimple failed). " +
                        "Use A1111 WebUI (e.g. port 7860) or a running ComfyUI on this URL.";
                }
            }
            catch (Exception ex)
            {
                StableDiffusionConnectionStatus = "✗ Failed";
                ConnectionTestResult = $"Image endpoint: ✗ Connection failed: {ex.Message}";
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        private void StartStabilityMatrix()
        {
            var path = (StabilityMatrixPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Set the Stability Matrix path in the field below (e.g. path to Stability Matrix.exe).", "Stability Matrix", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var exePath = path;
            if (Directory.Exists(path))
            {
                var candidates = new[] { "Stability Matrix.exe", "StabilityMatrix.exe" };
                foreach (var name in candidates)
                {
                    var combined = Path.Combine(path, name);
                    if (File.Exists(combined)) { exePath = combined; break; }
                }
            }
            if (!File.Exists(exePath))
            {
                MessageBox.Show($"Stability Matrix not found at:\n{path}", "Stability Matrix", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? path
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not start Stability Matrix: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartComfyUI()
        {
            var path = (ComfyUIPortablePath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Set the ComfyUI portable folder path below (folder containing run_nvidia_gpu.bat). You can use the ComfyUI install managed by Stability Matrix.", "ComfyUI", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!Directory.Exists(path))
            {
                MessageBox.Show($"ComfyUI folder not found:\n{path}", "ComfyUI", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var batPath = Path.Combine(path, "run_nvidia_gpu.bat");
            if (!File.Exists(batPath))
                batPath = Path.Combine(path, "run_cpu.bat");
            if (!File.Exists(batPath))
            {
                MessageBox.Show($"No run_nvidia_gpu.bat or run_cpu.bat found in:\n{path}", "ComfyUI", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                // Ensure D:\ComfyUI\models is loaded as extra models on startup
                WriteComfyUIExtraModelsConfig(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    UseShellExecute = true,
                    WorkingDirectory = path
                });
                MessageBox.Show("ComfyUI is starting. It usually runs at http://127.0.0.1:8188. Set the image endpoint above to http://localhost:8188 when using ComfyUI.", "ComfyUI", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not start ComfyUI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void WriteComfyUIExtraModelsConfig(string comfyUIRoot)
        {
            const string extraModelsYaml = @"# Extra models from D:\ComfyUI\models - loaded by House Victoria on ComfyUI startup
d_comfyui_models:
    base_path: D:\ComfyUI\models
    checkpoints: checkpoints
    text_encoders: text_encoders
    clip_vision: clip_vision
    configs: configs
    controlnet: controlnet
    diffusion_models: diffusion_models
    embeddings: embeddings
    loras: loras
    upscale_models: upscale_models
    vae: vae
";
            try
            {
                var destPath = Path.Combine(comfyUIRoot, "extra_model_paths.yaml");
                File.WriteAllText(destPath, extraModelsYaml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not write ComfyUI extra_model_paths.yaml: {ex.Message}");
            }
        }

        private void BrowseStabilityMatrixPath()
        {
            var path = (StabilityMatrixPath ?? string.Empty).Trim();
            var initialDir = !string.IsNullOrEmpty(path) && Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            var dialog = new OpenFileDialog
            {
                Title = "Select Stability Matrix executable",
                Filter = "Executable (*.exe)|*.exe|All Files (*.*)|*.*",
                FileName = "Stability Matrix.exe",
                InitialDirectory = initialDir
            };
            if (dialog.ShowDialog() == true)
                StabilityMatrixPath = dialog.FileName;
        }

        private void BrowseComfyUIPortablePath()
        {
            var path = (ComfyUIPortablePath ?? string.Empty).Trim();
            var initialDir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            var dialog = new OpenFileDialog
            {
                Title = "Select run_nvidia_gpu.bat or run_cpu.bat in the ComfyUI portable folder",
                Filter = "Batch files (*.bat)|*.bat|All Files (*.*)|*.*",
                FileName = "run_nvidia_gpu.bat",
                InitialDirectory = initialDir
            };
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.FileName))
                ComfyUIPortablePath = Path.GetDirectoryName(dialog.FileName) ?? path;
        }

        private void ImportSettings()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Import Settings",
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = "json"
                };

                if (dialog.ShowDialog() == true)
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var importedConfig = JsonSerializer.Deserialize<AppConfig>(json);

                    if (importedConfig != null)
                    {
                        // Load imported settings
                        LmStudioEndpoint = importedConfig.LmStudioEndpoint;
                        PrimaryLLM = importedConfig.PrimaryLLM;
                        OllamaEndpoint = importedConfig.OllamaEndpoint;
                        AnythingLLMEndpoint = importedConfig.AnythingLLMEndpoint;
                        MCPServerEndpoint = importedConfig.MCPServerEndpoint;
                        TTSEndpoint = importedConfig.TTSEndpoint;
                        STTEndpoint = importedConfig.STTEndpoint ?? string.Empty;
                        UnrealEngineEndpoint = importedConfig.UnrealEngineEndpoint;
                        StableDiffusionEndpoint = importedConfig.StableDiffusionEndpoint;
                        StabilityMatrixPath = importedConfig.StabilityMatrixPath ?? string.Empty;
                        ComfyUIPortablePath = importedConfig.ComfyUIPortablePath ?? string.Empty;
                        SelectedThemeIndex = ThemeManager.GetThemeIndexById(importedConfig.ColorScheme ?? "CyanBlueDark");
                        EnableOverlay = importedConfig.EnableOverlay;
                        OverlayOpacity = importedConfig.OverlayOpacity;
                        AutoHideTrays = importedConfig.AutoHideTrays;
                        AutoHideDelayMs = importedConfig.AutoHideDelayMs;
                        WalkSpeed = importedConfig.WalkSpeed;
                        RunSpeed = importedConfig.RunSpeed;
                        JumpHeight = importedConfig.JumpHeight;
                        EnablePhysicsInteraction = importedConfig.EnablePhysicsInteraction;
                        EnableFileSystemAccess = importedConfig.EnableFileSystemAccess;
                        EnableNetworkAccess = importedConfig.EnableNetworkAccess;
                        EnableSystemCommands = importedConfig.EnableSystemCommands;
                        EnablePersistentMemory = importedConfig.EnablePersistentMemory;
                        PersistentMemoryPath = importedConfig.PersistentMemoryPath;
                        MemoryMaxEntries = importedConfig.MemoryMaxEntries;
                        MemoryImportanceThreshold = importedConfig.MemoryImportanceThreshold;
                        MemoryRetentionDays = importedConfig.MemoryRetentionDays;

                        MessageBox.Show("Settings imported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportSettings()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Export Settings",
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = "HouseVictoria_Settings.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    // Create export config with current settings
                    var exportConfig = new AppConfig
                    {
                        LmStudioEndpoint = LmStudioEndpoint,
                        PrimaryLLM = PrimaryLLM,
                        OllamaEndpoint = OllamaEndpoint,
                        AnythingLLMEndpoint = AnythingLLMEndpoint,
                        MCPServerEndpoint = MCPServerEndpoint,
                        TTSEndpoint = TTSEndpoint,
                        STTEndpoint = string.IsNullOrWhiteSpace(STTEndpoint) ? null : STTEndpoint,
                        UnrealEngineEndpoint = UnrealEngineEndpoint,
                        StableDiffusionEndpoint = StableDiffusionEndpoint,
                        StabilityMatrixPath = StabilityMatrixPath,
                        ComfyUIPortablePath = ComfyUIPortablePath,
                        ColorScheme = ThemeManager.GetThemeIdByIndex(SelectedThemeIndex),
                        EnableOverlay = EnableOverlay,
                        OverlayOpacity = OverlayOpacity,
                        AutoHideTrays = AutoHideTrays,
                        AutoHideDelayMs = AutoHideDelayMs,
                        WalkSpeed = WalkSpeed,
                        RunSpeed = RunSpeed,
                        JumpHeight = JumpHeight,
                        EnablePhysicsInteraction = EnablePhysicsInteraction,
                        EnableFileSystemAccess = EnableFileSystemAccess,
                        EnableNetworkAccess = EnableNetworkAccess,
                        EnableSystemCommands = EnableSystemCommands,
                        EnablePersistentMemory = EnablePersistentMemory,
                        PersistentMemoryPath = PersistentMemoryPath,
                        MemoryMaxEntries = MemoryMaxEntries,
                        MemoryImportanceThreshold = MemoryImportanceThreshold,
                        MemoryRetentionDays = MemoryRetentionDays
                    };

                    var json = JsonSerializer.Serialize(exportConfig, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dialog.FileName, json);

                    MessageBox.Show($"Settings exported successfully to:\n{dialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SaveSettings()
        {
            if (!IsValid)
            {
                MessageBox.Show($"Cannot save settings: {ValidationError}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Update AppConfig
                _appConfig.LmStudioEndpoint = LmStudioEndpoint;
                _appConfig.PrimaryLLM = PrimaryLLM;
                _appConfig.OllamaEndpoint = OllamaEndpoint;
                _appConfig.AnythingLLMEndpoint = AnythingLLMEndpoint;
                _appConfig.MCPServerEndpoint = MCPServerEndpoint;
                _appConfig.TTSEndpoint = TTSEndpoint;
                _appConfig.STTEndpoint = string.IsNullOrWhiteSpace(STTEndpoint) ? null : STTEndpoint;
                _appConfig.UnrealEngineEndpoint = UnrealEngineEndpoint;
                _appConfig.StableDiffusionEndpoint = StableDiffusionEndpoint;
                _appConfig.StabilityMatrixPath = StabilityMatrixPath;
                _appConfig.ComfyUIPortablePath = ComfyUIPortablePath;
                _appConfig.ColorScheme = ThemeManager.GetThemeIdByIndex(SelectedThemeIndex);
                _appConfig.EnableOverlay = EnableOverlay;
                _appConfig.OverlayOpacity = OverlayOpacity;
                _appConfig.AutoHideTrays = AutoHideTrays;
                _appConfig.AutoHideDelayMs = AutoHideDelayMs;
                _appConfig.WalkSpeed = WalkSpeed;
                _appConfig.RunSpeed = RunSpeed;
                _appConfig.JumpHeight = JumpHeight;
                _appConfig.EnablePhysicsInteraction = EnablePhysicsInteraction;
                _appConfig.EnableFileSystemAccess = EnableFileSystemAccess;
                _appConfig.EnableNetworkAccess = EnableNetworkAccess;
                _appConfig.EnableSystemCommands = EnableSystemCommands;
                _appConfig.EnablePersistentMemory = EnablePersistentMemory;
                _appConfig.PersistentMemoryPath = PersistentMemoryPath;
                _appConfig.MemoryMaxEntries = MemoryMaxEntries;
                _appConfig.MemoryImportanceThreshold = MemoryImportanceThreshold;
                _appConfig.MemoryRetentionDays = MemoryRetentionDays;

                // Save to App.config file (basic settings only)
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                
                UpdateOrAddSetting(config, "LmStudioEndpoint", LmStudioEndpoint);
                UpdateOrAddSetting(config, "PrimaryLLM", PrimaryLLM);
                UpdateOrAddSetting(config, "OllamaEndpoint", OllamaEndpoint);
                UpdateOrAddSetting(config, "AnythingLLMEndpoint", AnythingLLMEndpoint);
                UpdateOrAddSetting(config, "MCPServerEndpoint", MCPServerEndpoint);
                UpdateOrAddSetting(config, "TTSEndpoint", TTSEndpoint);
                UpdateOrAddSetting(config, "STTEndpoint", STTEndpoint ?? string.Empty);
                UpdateOrAddSetting(config, "UnrealEngineEndpoint", UnrealEngineEndpoint);
                UpdateOrAddSetting(config, "StableDiffusionEndpoint", StableDiffusionEndpoint);
                UpdateOrAddSetting(config, "StabilityMatrixPath", StabilityMatrixPath ?? string.Empty);
                UpdateOrAddSetting(config, "ComfyUIPortablePath", ComfyUIPortablePath ?? string.Empty);
                UpdateOrAddSetting(config, "ColorScheme", ThemeManager.GetThemeIdByIndex(SelectedThemeIndex));
                UpdateOrAddSetting(config, "EnableOverlay", EnableOverlay.ToString());
                UpdateOrAddSetting(config, "OverlayOpacity", OverlayOpacity.ToString());
                UpdateOrAddSetting(config, "AutoHideTrays", AutoHideTrays.ToString());
                UpdateOrAddSetting(config, "AutoHideDelayMs", AutoHideDelayMs.ToString());

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                // Apply theme immediately
                ThemeManager.ApplyTheme(ThemeManager.GetThemeIdByIndex(SelectedThemeIndex));

                // Write primary-llm.txt and comfyui-portable-path.txt for start.bat to read
                try
                {
                    var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
                    var dir = new DirectoryInfo(appDir);
                    for (int i = 0; i < 6 && dir != null; i++)
                    {
                        if (File.Exists(Path.Combine(dir.FullName, "HouseVictoria.sln")))
                        {
                            File.WriteAllText(Path.Combine(dir.FullName, "primary-llm.txt"), PrimaryLLM);
                            var comfyPath = Path.Combine(dir.FullName, "comfyui-portable-path.txt");
                            if (!string.IsNullOrWhiteSpace(ComfyUIPortablePath))
                                File.WriteAllText(comfyPath, ComfyUIPortablePath.Trim());
                            else if (File.Exists(comfyPath))
                                File.Delete(comfyPath);
                            break;
                        }
                        dir = dir.Parent;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not write primary-llm.txt / comfyui-portable-path.txt: {ex.Message}");
                }

                MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateOrAddSetting(Configuration config, string key, string value)
        {
            if (config.AppSettings.Settings[key] != null)
            {
                config.AppSettings.Settings[key].Value = value;
            }
            else
            {
                config.AppSettings.Settings.Add(key, value);
            }
        }

        private void ResetToDefaults()
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to their default values? This action cannot be undone.",
                "Reset to Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Reset to default values
                var defaults = new AppConfig();

                LmStudioEndpoint = defaults.LmStudioEndpoint;
                PrimaryLLM = defaults.PrimaryLLM;
                OllamaEndpoint = defaults.OllamaEndpoint;
                AnythingLLMEndpoint = defaults.AnythingLLMEndpoint;
                MCPServerEndpoint = defaults.MCPServerEndpoint;
                TTSEndpoint = defaults.TTSEndpoint;
                STTEndpoint = defaults.STTEndpoint ?? string.Empty;
                UnrealEngineEndpoint = defaults.UnrealEngineEndpoint;
                StableDiffusionEndpoint = defaults.StableDiffusionEndpoint;
                StabilityMatrixPath = defaults.StabilityMatrixPath ?? string.Empty;
                ComfyUIPortablePath = defaults.ComfyUIPortablePath ?? string.Empty;
                SelectedThemeIndex = ThemeManager.GetThemeIndexById(defaults.ColorScheme ?? "CyanBlueDark");
                EnableOverlay = defaults.EnableOverlay;
                OverlayOpacity = defaults.OverlayOpacity;
                AutoHideTrays = defaults.AutoHideTrays;
                AutoHideDelayMs = defaults.AutoHideDelayMs;
                WalkSpeed = defaults.WalkSpeed;
                RunSpeed = defaults.RunSpeed;
                JumpHeight = defaults.JumpHeight;
                EnablePhysicsInteraction = defaults.EnablePhysicsInteraction;
                EnableFileSystemAccess = defaults.EnableFileSystemAccess;
                EnableNetworkAccess = defaults.EnableNetworkAccess;
                EnableSystemCommands = defaults.EnableSystemCommands;
                EnablePersistentMemory = defaults.EnablePersistentMemory;
                PersistentMemoryPath = defaults.PersistentMemoryPath;
                MemoryMaxEntries = defaults.MemoryMaxEntries;
                MemoryImportanceThreshold = defaults.MemoryImportanceThreshold;
                MemoryRetentionDays = defaults.MemoryRetentionDays;

                // Clear connection statuses
                LmStudioConnectionStatus = null;
                OllamaConnectionStatus = null;
                AnythingLLMConnectionStatus = null;
                MCPServerConnectionStatus = null;
                TTSConnectionStatus = null;
                STTConnectionStatus = null;
                UnrealConnectionStatus = null;
                StableDiffusionConnectionStatus = null;
                ConnectionTestResult = null;

                MessageBox.Show("Settings have been reset to default values.", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
