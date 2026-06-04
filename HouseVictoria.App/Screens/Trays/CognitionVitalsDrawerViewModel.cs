using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Events;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Trays
{
    public class CognitionVitalsDrawerViewModel : ObservableObject, IDisposable
    {
        private readonly System.Windows.Controls.Border _drawerPanel;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _refreshTimer;
        private readonly IAutonomyService? _autonomyService;
        private readonly ITradingService? _tradingService;
        private readonly IPersonaContext? _personaContext;
        private DateTime _lastTradingVitalsPollUtc = DateTime.MinValue;
        private bool _isDrawerOpen;
        public bool IsDrawerOpen
        {
            get => _isDrawerOpen;
            set
            {
                if (SetProperty(ref _isDrawerOpen, value))
                    _drawerPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private string _activeAiName = "AI";
        public string ActiveAiName
        {
            get => _activeAiName;
            set => SetProperty(ref _activeAiName, value);
        }

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

        private string _autonomyStatusText = "Stopped";
        public string AutonomyStatusText
        {
            get => _autonomyStatusText;
            set => SetProperty(ref _autonomyStatusText, value);
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

        public ICommand ToggleDrawerCommand { get; }

        public CognitionVitalsDrawerViewModel(System.Windows.Controls.Border drawerPanel)
        {
            _drawerPanel = drawerPanel ?? throw new ArgumentNullException(nameof(drawerPanel));
            _dispatcher = drawerPanel.Dispatcher;

            try
            {
                _autonomyService = App.GetService<IAutonomyService>();
                _tradingService = App.GetService<ITradingService>();
                _personaContext = App.GetService<IPersonaContext>();

                if (_autonomyService != null)
                {
                    _autonomyService.VitalsChanged += OnAutonomyVitalsChanged;
                    RunOnUiThread(() => ApplyVitals(_autonomyService.GetVitals()));
                }

                if (_personaContext != null)
                {
                    _personaContext.PrimaryChanged += OnPrimaryPersonaChanged;
                    _ = RefreshActiveAiNameAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CognitionVitalsDrawer: service init failed: {ex.Message}");
            }

            ToggleDrawerCommand = new RelayCommand(() => IsDrawerOpen = !IsDrawerOpen);
            _drawerPanel.Visibility = Visibility.Collapsed;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _refreshTimer.Tick += (_, _) => RefreshLiveFields();
            _refreshTimer.Start();
        }

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

        private void OnAutonomyVitalsChanged(object? sender, CognitionVitalsChangedEventArgs e) =>
            RunOnUiThread(() => ApplyVitals(e.Vitals));

        private void OnPrimaryPersonaChanged(object? sender, PersonaChangedEvent e) =>
            RunOnUiThread(() => _ = RefreshActiveAiNameAsync());

        private async Task RefreshActiveAiNameAsync()
        {
            if (_personaContext == null)
                return;

            try
            {
                var contact = await _personaContext.GetPrimaryAsync().ConfigureAwait(false);
                var name = contact?.Name?.Trim();
                RunOnUiThread(() => ActiveAiName = string.IsNullOrWhiteSpace(name) ? "AI" : name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CognitionVitalsDrawer persona: {ex.Message}");
            }
        }

        private void RefreshLiveFields()
        {
            if (_autonomyService == null)
                return;

            try
            {
                ApplyVitals(_autonomyService.GetVitals());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cognition vitals refresh: {ex.Message}");
            }
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
            AutonomyStatusText = vitals.AutonomyRunning ? "Running" : "Stopped";

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
            if (_autonomyService != null)
                _autonomyService.VitalsChanged -= OnAutonomyVitalsChanged;
            if (_personaContext != null)
                _personaContext.PrimaryChanged -= OnPrimaryPersonaChanged;
        }
    }
}
