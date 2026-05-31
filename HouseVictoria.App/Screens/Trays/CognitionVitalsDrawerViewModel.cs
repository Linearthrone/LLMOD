using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Trays
{
    public class CognitionVitalsDrawerViewModel : ObservableObject, IDisposable
    {
        private readonly System.Windows.Controls.Border _drawerPanel;
        private readonly Dispatcher _dispatcher;
        private readonly IAutonomyService? _autonomyService;
        private readonly ITradingService? _tradingService;
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

        private string _cognitionStatusLabel = "At rest";
        public string CognitionStatusLabel
        {
            get => _cognitionStatusLabel;
            set => SetProperty(ref _cognitionStatusLabel, value);
        }

        private string _autonomyLastActivityText = "No autonomy activity yet";
        public string AutonomyLastActivityText
        {
            get => _autonomyLastActivityText;
            set => SetProperty(ref _autonomyLastActivityText, value);
        }

        private bool _autonomyRunning;
        public bool AutonomyRunning
        {
            get => _autonomyRunning;
            set => SetProperty(ref _autonomyRunning, value);
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

                if (_autonomyService != null)
                {
                    _autonomyService.VitalsChanged += OnAutonomyVitalsChanged;
                    _autonomyService.ActivityCompleted += OnAutonomyActivityCompleted;
                    RunOnUiThread(() => ApplyVitals(_autonomyService.GetVitals()));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CognitionVitalsDrawer: service init failed: {ex.Message}");
            }

            ToggleDrawerCommand = new RelayCommand(() => IsDrawerOpen = !IsDrawerOpen);
            _drawerPanel.Visibility = Visibility.Collapsed;
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

                // Only treat this as "active trading" when MT4 is genuinely live AND actually holding
                // positions. A merely-connected (or idle) MT4 must NOT hijack her cognition rhythm,
                // otherwise the heart monitor gets pinned to "Active trading" forever.
                if (!status.IsConnected || !status.IsBridgeActive)
                    return;

                var positions = await _tradingService.GetOpenPositionsAsync().ConfigureAwait(false);
                if (positions.Count > 0)
                {
                    // Short override (~8s) so it lapses quickly once trading stops and her real
                    // autonomy rhythm can show through again. Poll runs every 5s, so it stays lit
                    // only while positions are genuinely open.
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

        private void OnAutonomyActivityCompleted(object? sender, AutonomyActivityEventArgs e) =>
            RunOnUiThread(() => AutonomyLastActivityText = $"{e.Activity}: {e.Summary}");

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
            CognitionStatusLabel = vitals.Label;
            AutonomyRunning = vitals.AutonomyRunning;
            if (!string.IsNullOrWhiteSpace(vitals.LastActivitySummary))
                AutonomyLastActivityText = vitals.LastActivitySummary;
        }

        public void Dispose()
        {
            if (_autonomyService != null)
            {
                _autonomyService.VitalsChanged -= OnAutonomyVitalsChanged;
                _autonomyService.ActivityCompleted -= OnAutonomyActivityCompleted;
            }
        }
    }
}
