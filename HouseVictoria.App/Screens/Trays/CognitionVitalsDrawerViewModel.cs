using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Events;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Core.Utils;
using HouseVictoria.Services.Autonomy;

namespace HouseVictoria.App.Screens.Trays
{
    public enum VitalsDrawerCollapseState
    {
        Handle,
        Pulse,
        Open
    }

    public sealed class AutonomyLogLineViewModel
    {
        public string Timestamp { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
    }

    public class CognitionVitalsDrawerViewModel : ObservableObject, IDisposable
    {
        private static readonly TimeSpan ActionLogWindow = TimeSpan.FromHours(24);

        private readonly System.Windows.Controls.Border _drawerPanel;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _logTimer;
        private readonly IAutonomyService? _autonomyService;
        private readonly ITradingService? _tradingService;
        private readonly IPersonaContext? _personaContext;
        private readonly AppConfig? _appConfig;
        private DateTime _lastTradingVitalsPollUtc = DateTime.MinValue;
        private int _selectedTabIndex;

        public CognitionVitalsDrawerViewModel(System.Windows.Controls.Border drawerPanel)
        {
            _drawerPanel = drawerPanel ?? throw new ArgumentNullException(nameof(drawerPanel));
            _dispatcher = drawerPanel.Dispatcher;
            ActionLogLines = new ObservableCollection<AutonomyLogLineViewModel>();

            try
            {
                _appConfig = App.GetService<AppConfig>();
                _autonomyService = App.GetService<IAutonomyService>();
                _tradingService = App.GetService<ITradingService>();
                _personaContext = App.GetService<IPersonaContext>();

                if (_autonomyService != null)
                {
                    _autonomyService.VitalsChanged += OnAutonomyVitalsChanged;
                    _autonomyService.AutonomyLevelChanged += OnAutonomyLevelChanged;
                    _autonomyService.ActionLogChanged += OnActionLogChanged;
                    RunOnUiThread(() =>
                    {
                        ApplyVitals(_autonomyService.GetVitals());
                        LoadSettingsFromConfig();
                        RefreshControlSnapshot();
                        RefreshActionLog();
                        AutonomySuggestion = _autonomyService.GetUserGuidanceSuggestion() ?? string.Empty;
                    });
                }
                else if (_appConfig != null)
                {
                    LoadSettingsFromConfig();
                    RefreshAutonomyLevel();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CognitionVitalsDrawer: service init failed: {ex.Message}");
            }

            ExpandFromHandleCommand = new RelayCommand(() => CollapseState = VitalsDrawerCollapseState.Pulse);
            CycleAutonomyLevelCommand = new RelayCommand(() => _ = CycleAutonomyLevelAsync());
            ApplyAutonomySuggestionCommand = new RelayCommand(() => _ = ApplyGuidanceAsync());
            ClearGuidanceCommand = new RelayCommand(() => _ = ClearGuidanceAsync());
            SaveAutonomySettingsCommand = new RelayCommand(() => _ = SaveAutonomySettingsAsync());
            RestartAutonomyCommand = new RelayCommand(() => _ = RestartAutonomyAsync());
            RefreshActionLogCommand = new RelayCommand(RefreshActionLog);

            _drawerPanel.Visibility = Visibility.Collapsed;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _refreshTimer.Tick += (_, _) => RefreshLiveFields();
            _refreshTimer.Start();

            _logTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _logTimer.Tick += (_, _) =>
            {
                if (SelectedTabIndex == 1)
                    RefreshActionLog();
            };
            _logTimer.Start();
        }

        public ObservableCollection<AutonomyLogLineViewModel> ActionLogLines { get; }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (!SetProperty(ref _selectedTabIndex, value))
                    return;
                if (value == 1)
                    RefreshActionLog();
            }
        }

        private VitalsDrawerCollapseState _collapseState = VitalsDrawerCollapseState.Pulse;
        public VitalsDrawerCollapseState CollapseState
        {
            get => _collapseState;
            set
            {
                if (!SetProperty(ref _collapseState, value))
                    return;

                _drawerPanel.Visibility = value == VitalsDrawerCollapseState.Open
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                OnPropertyChanged(nameof(IsHandleMode));
                OnPropertyChanged(nameof(IsPulseMode));
                OnPropertyChanged(nameof(IsOpenMode));
            }
        }

        public bool IsHandleMode => CollapseState == VitalsDrawerCollapseState.Handle;
        public bool IsPulseMode => CollapseState == VitalsDrawerCollapseState.Pulse;
        public bool IsOpenMode => CollapseState == VitalsDrawerCollapseState.Open;

        private CognitionVitalRhythm _cognitionRhythm = CognitionVitalRhythm.Resting;
        public CognitionVitalRhythm CognitionRhythm
        {
            get => _cognitionRhythm;
            set => SetProperty(ref _cognitionRhythm, value);
        }

        private double _cognitionBpm = 52;
        public double CognitionBpm
        {
            get => _cognitionBpm;
            set => SetProperty(ref _cognitionBpm, value);
        }

        private double _cognitionIntensity = 0.25;
        public double CognitionIntensity
        {
            get => _cognitionIntensity;
            set => SetProperty(ref _cognitionIntensity, value);
        }

        private string _cognitionWaveColorHex = "#4FC3F7";
        public string CognitionWaveColorHex
        {
            get => _cognitionWaveColorHex;
            set => SetProperty(ref _cognitionWaveColorHex, value);
        }

        private string _cognitionRhythmDescription = "At rest";
        public string CognitionRhythmDescription
        {
            get => _cognitionRhythmDescription;
            set => SetProperty(ref _cognitionRhythmDescription, value);
        }

        private string _autonomyLevelLabel = "Mid";
        public string AutonomyLevelLabel
        {
            get => _autonomyLevelLabel;
            set => SetProperty(ref _autonomyLevelLabel, value);
        }

        private string _autonomyStatusText = "Stopped";
        public string AutonomyStatusText
        {
            get => _autonomyStatusText;
            set => SetProperty(ref _autonomyStatusText, value);
        }

        private string _autonomySuggestion = string.Empty;
        public string AutonomySuggestion
        {
            get => _autonomySuggestion;
            set => SetProperty(ref _autonomySuggestion, value);
        }

        private string _currentActivityDescription = "No activity yet";
        public string CurrentActivityDescription
        {
            get => _currentActivityDescription;
            set => SetProperty(ref _currentActivityDescription, value);
        }

        private string _currentActivityElapsed = "—";
        public string CurrentActivityElapsed
        {
            get => _currentActivityElapsed;
            set => SetProperty(ref _currentActivityElapsed, value);
        }

        private string _previousActivityDescription = "—";
        public string PreviousActivityDescription
        {
            get => _previousActivityDescription;
            set => SetProperty(ref _previousActivityDescription, value);
        }

        private string _previousActivityDuration = "—";
        public string PreviousActivityDuration
        {
            get => _previousActivityDuration;
            set => SetProperty(ref _previousActivityDuration, value);
        }

        private bool _enableAutonomy = true;
        public bool EnableAutonomy
        {
            get => _enableAutonomy;
            set => SetProperty(ref _enableAutonomy, value);
        }

        private int _tickIntervalSeconds = 90;
        public int TickIntervalSeconds
        {
            get => _tickIntervalSeconds;
            set => SetProperty(ref _tickIntervalSeconds, value);
        }

        private int _minIdleMinutes = 2;
        public int MinIdleMinutes
        {
            get => _minIdleMinutes;
            set => SetProperty(ref _minIdleMinutes, value);
        }

        private int _highPriorityThreshold = 7;
        public int HighPriorityThreshold
        {
            get => _highPriorityThreshold;
            set => SetProperty(ref _highPriorityThreshold, value);
        }

        private bool _enableArtGeneration = true;
        public bool EnableArtGeneration
        {
            get => _enableArtGeneration;
            set => SetProperty(ref _enableArtGeneration, value);
        }

        private int _maxActionsPerHour = 6;
        public int MaxActionsPerHour
        {
            get => _maxActionsPerHour;
            set => SetProperty(ref _maxActionsPerHour, value);
        }

        private int _maxArtPerHour = 2;
        public int MaxArtPerHour
        {
            get => _maxArtPerHour;
            set => SetProperty(ref _maxArtPerHour, value);
        }

        private bool _enableSelfGoals = true;
        public bool EnableSelfGoals
        {
            get => _enableSelfGoals;
            set => SetProperty(ref _enableSelfGoals, value);
        }

        private int _maxSelfGoalsPerDay = 3;
        public int MaxSelfGoalsPerDay
        {
            get => _maxSelfGoalsPerDay;
            set => SetProperty(ref _maxSelfGoalsPerDay, value);
        }

        private double _selfGoalDriveThreshold = 0.65;
        public double SelfGoalDriveThreshold
        {
            get => _selfGoalDriveThreshold;
            set => SetProperty(ref _selfGoalDriveThreshold, value);
        }

        private int _maxActiveSelfProjects = 3;
        public int MaxActiveSelfProjects
        {
            get => _maxActiveSelfProjects;
            set => SetProperty(ref _maxActiveSelfProjects, value);
        }

        private int _userGuidanceMaxTicks = 3;
        public int UserGuidanceMaxTicks
        {
            get => _userGuidanceMaxTicks;
            set => SetProperty(ref _userGuidanceMaxTicks, value);
        }

        private string _drivesSummary = "—";
        public string DrivesSummary
        {
            get => _drivesSummary;
            set => SetProperty(ref _drivesSummary, value);
        }

        private string _interestsSummary = "—";
        public string InterestsSummary
        {
            get => _interestsSummary;
            set => SetProperty(ref _interestsSummary, value);
        }

        private string _budgetSummary = "—";
        public string BudgetSummary
        {
            get => _budgetSummary;
            set => SetProperty(ref _budgetSummary, value);
        }

        private string _guidanceStatus = "—";
        public string GuidanceStatus
        {
            get => _guidanceStatus;
            set => SetProperty(ref _guidanceStatus, value);
        }

        private string _actionLogHeader = "Last 24 hours";
        public string ActionLogHeader
        {
            get => _actionLogHeader;
            set => SetProperty(ref _actionLogHeader, value);
        }

        public ICommand ExpandFromHandleCommand { get; }
        public ICommand CycleAutonomyLevelCommand { get; }
        public ICommand ApplyAutonomySuggestionCommand { get; }
        public ICommand ClearGuidanceCommand { get; }
        public ICommand SaveAutonomySettingsCommand { get; }
        public ICommand RestartAutonomyCommand { get; }
        public ICommand RefreshActionLogCommand { get; }

        public void CollapseToHandle() => CollapseState = VitalsDrawerCollapseState.Handle;
        public void OpenDrawer() => CollapseState = VitalsDrawerCollapseState.Open;
        public void CollapseToPulse() => CollapseState = VitalsDrawerCollapseState.Pulse;

        public async Task PollTradingVitalsAsync()
        {
            if (_tradingService == null || _autonomyService == null)
                return;

            if (DateTime.UtcNow - _lastTradingVitalsPollUtc < TimeSpan.FromSeconds(5))
                return;

            _lastTradingVitalsPollUtc = DateTime.UtcNow;

            try
            {
                var status = await _tradingService.GetStatusAsync().ConfigureAwait(false);
                if (!status.IsConnected || !status.IsBridgeActive)
                    return;

                var positions = await _tradingService.GetOpenPositionsAsync().ConfigureAwait(false);
                if (positions.Count > 0)
                {
                    _autonomyService.PushVitalOverride(
                        CognitionVitalRhythm.TradingActive,
                        $"Trading · {positions.Count} open position(s)",
                        TimeSpan.FromSeconds(8));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Trading vitals poll: {ex.Message}");
            }
        }

        private void LoadSettingsFromConfig()
        {
            if (_appConfig == null)
                return;

            EnableAutonomy = _appConfig.EnableAutonomy;
            TickIntervalSeconds = _appConfig.AutonomyTickIntervalSeconds;
            MinIdleMinutes = _appConfig.AutonomyMinIdleMinutes;
            HighPriorityThreshold = _appConfig.AutonomyHighPriorityThreshold;
            EnableArtGeneration = _appConfig.AutonomyEnableArtGeneration;
            MaxActionsPerHour = _appConfig.AutonomyMaxActionsPerHour;
            MaxArtPerHour = _appConfig.AutonomyMaxArtPerHour;
            EnableSelfGoals = _appConfig.AutonomyEnableSelfGoals;
            MaxSelfGoalsPerDay = _appConfig.AutonomyMaxSelfGoalsPerDay;
            SelfGoalDriveThreshold = _appConfig.AutonomySelfGoalDriveThreshold;
            MaxActiveSelfProjects = _appConfig.AutonomyMaxActiveSelfProjects;
            UserGuidanceMaxTicks = _appConfig.AutonomyUserGuidanceMaxTicks;
            RefreshAutonomyLevel();
        }

        private async Task CycleAutonomyLevelAsync()
        {
            if (_autonomyService == null || _appConfig == null)
                return;

            try
            {
                var next = AutonomyLevelProfile.Cycle(_autonomyService.GetAutonomyLevel());
                await _autonomyService.SetAutonomyLevelAsync(next).ConfigureAwait(true);
                _appConfig.AutonomyLevel = next;
                PersistConfig();
                RunOnUiThread(RefreshAutonomyLevel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cycle autonomy level: {ex.Message}");
            }
        }

        private async Task ApplyGuidanceAsync()
        {
            if (_autonomyService == null)
                return;

            await _autonomyService.ApplySettingsAsync(new AutonomySettingsUpdate
            {
                UserGuidance = AutonomySuggestion
            }).ConfigureAwait(true);

            RunOnUiThread(RefreshControlSnapshot);
        }

        private async Task ClearGuidanceAsync()
        {
            if (_autonomyService == null)
                return;

            AutonomySuggestion = string.Empty;
            await _autonomyService.ApplySettingsAsync(new AutonomySettingsUpdate
            {
                ClearUserGuidance = true
            }).ConfigureAwait(true);

            RunOnUiThread(RefreshControlSnapshot);
        }

        private async Task SaveAutonomySettingsAsync()
        {
            if (_autonomyService == null || _appConfig == null)
                return;

            try
            {
                await _autonomyService.ApplySettingsAsync(new AutonomySettingsUpdate
                {
                    EnableAutonomy = EnableAutonomy,
                    TickIntervalSeconds = TickIntervalSeconds,
                    MinIdleMinutes = MinIdleMinutes,
                    HighPriorityThreshold = HighPriorityThreshold,
                    EnableArtGeneration = EnableArtGeneration,
                    MaxActionsPerHour = MaxActionsPerHour,
                    MaxArtPerHour = MaxArtPerHour,
                    EnableSelfGoals = EnableSelfGoals,
                    MaxSelfGoalsPerDay = MaxSelfGoalsPerDay,
                    SelfGoalDriveThreshold = SelfGoalDriveThreshold,
                    MaxActiveSelfProjects = MaxActiveSelfProjects,
                    UserGuidanceMaxTicks = UserGuidanceMaxTicks,
                    UserGuidance = AutonomySuggestion
                }).ConfigureAwait(true);

                LoadSettingsFromConfig();
                RunOnUiThread(RefreshControlSnapshot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save autonomy settings: {ex.Message}");
            }
        }

        private async Task RestartAutonomyAsync()
        {
            if (_autonomyService == null)
                return;

            try
            {
                await _autonomyService.RestartLoopAsync().ConfigureAwait(true);
                RunOnUiThread(() =>
                {
                    RefreshControlSnapshot();
                    RefreshActionLog();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Restart autonomy: {ex.Message}");
            }
        }

        private void PersistConfig()
        {
            if (_appConfig == null)
                return;

            try
            {
                UserSettingsStore.Save(_appConfig);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Persist config: {ex.Message}");
            }
        }

        private void OnAutonomyVitalsChanged(object? sender, CognitionVitalsChangedEventArgs e) =>
            RunOnUiThread(() => ApplyVitals(e.Vitals));

        private void OnAutonomyLevelChanged(object? sender, EventArgs e) =>
            RunOnUiThread(RefreshAutonomyLevel);

        private void OnActionLogChanged(object? sender, EventArgs e) =>
            RunOnUiThread(RefreshActionLog);

        private void RefreshLiveFields()
        {
            if (_autonomyService == null)
                return;

            try
            {
                ApplyVitals(_autonomyService.GetVitals());
                if (SelectedTabIndex == 1)
                    RefreshControlSnapshot();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cognition vitals refresh: {ex.Message}");
            }
        }

        private void RefreshControlSnapshot()
        {
            if (_autonomyService == null)
                return;

            var snap = _autonomyService.GetControlSnapshot();
            DrivesSummary = string.Join(" · ", snap.Drives.Select(kv => $"{kv.Key} {kv.Value:F2}"));
            InterestsSummary = snap.Interests.Count == 0
                ? "(none yet)"
                : string.Join(", ", snap.Interests.Select(i =>
                    i.IsActive ? $"{i.Tag}*{i.Weight:F2}" : $"{i.Tag} {i.Weight:F2}"));
            BudgetSummary =
                $"Actions {snap.ActionsThisHour}/{snap.MaxActionsPerHour} · Art {snap.ArtGeneratedThisHour}/{snap.MaxArtPerHour} · Self-goals {snap.SelfGoalsToday}/{snap.MaxSelfGoalsPerDay} · Ticks {snap.TotalTicks} · Done {snap.TotalActions}";
            GuidanceStatus = string.IsNullOrWhiteSpace(snap.UserGuidance)
                ? "No active guidance"
                : $"Active ({snap.UserGuidanceTicksRemaining} ticks left): {snap.UserGuidance}";
        }

        private void RefreshActionLog()
        {
            if (_autonomyService == null)
                return;

            try
            {
                var entries = _autonomyService.GetRecentActionLog(ActionLogWindow);
                ActionLogHeader = $"Last 24 hours ({entries.Count} entries)";

                ActionLogLines.Clear();
                foreach (var entry in entries.Take(120))
                {
                    ActionLogLines.Add(new AutonomyLogLineViewModel
                    {
                        Timestamp = entry.TimestampLocal.ToString("MM-dd HH:mm"),
                        Text = entry.Text
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh action log: {ex.Message}");
            }
        }

        private void RefreshAutonomyLevel()
        {
            var level = _autonomyService?.GetAutonomyLevel() ?? _appConfig?.AutonomyLevel ?? AutonomyLevel.Mid;
            AutonomyLevelLabel = AutonomyLevelProfile.DisplayLabel(level);
        }

        private void RunOnUiThread(Action action)
        {
            if (_dispatcher.CheckAccess())
                action();
            else
                _dispatcher.Invoke(action);
        }

        private void ApplyVitals(CognitionVitalsSnapshot vitals)
        {
            CognitionRhythm = vitals.Rhythm;
            CognitionBpm = vitals.BeatsPerMinute;
            CognitionIntensity = vitals.Intensity;
            CognitionWaveColorHex = vitals.WaveColorHex;
            CognitionRhythmDescription = string.IsNullOrWhiteSpace(vitals.Label) ? "Present" : vitals.Label;

            var levelLabel = AutonomyLevelLabel;
            AutonomyStatusText = vitals.AutonomyRunning
                ? $"{levelLabel} · Running"
                : levelLabel == "Off" ? "Off" : $"{levelLabel} · Stopped";

            CurrentActivityDescription = string.IsNullOrWhiteSpace(vitals.LastActivitySummary)
                ? "No activity yet"
                : vitals.LastActivitySummary;

            PreviousActivityDescription = string.IsNullOrWhiteSpace(vitals.PreviousActivitySummary)
                ? "—"
                : vitals.PreviousActivitySummary;

            CurrentActivityElapsed = FormatElapsed(vitals.CurrentActivityStartedUtc, DateTime.UtcNow);
            PreviousActivityDuration = FormatDuration(vitals.PreviousActivityStartedUtc, vitals.PreviousActivityEndedUtc);
        }

        private static string FormatElapsed(DateTime? startedUtc, DateTime nowUtc)
        {
            if (!startedUtc.HasValue)
                return "—";

            return FormatTimeSpan(nowUtc - startedUtc.Value);
        }

        private static string FormatDuration(DateTime? startedUtc, DateTime? endedUtc)
        {
            if (!startedUtc.HasValue || !endedUtc.HasValue)
                return "—";

            var span = endedUtc.Value - startedUtc.Value;
            return span <= TimeSpan.Zero ? "—" : FormatTimeSpan(span);
        }

        private static string FormatTimeSpan(TimeSpan span)
        {
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours}h {span.Minutes}m";
            if (span.TotalMinutes >= 1)
                return $"{(int)span.TotalMinutes}m {span.Seconds}s";
            return $"{Math.Max(0, span.Seconds)}s";
        }

        public void Dispose()
        {
            _refreshTimer.Stop();
            _logTimer.Stop();
            if (_autonomyService != null)
            {
                _autonomyService.VitalsChanged -= OnAutonomyVitalsChanged;
                _autonomyService.AutonomyLevelChanged -= OnAutonomyLevelChanged;
                _autonomyService.ActionLogChanged -= OnActionLogChanged;
            }
        }
    }
}
