using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HouseVictoria.Core.Interfaces;

namespace HouseVictoria.App.Screens.Trays
{
    public partial class InstrumentStackDrawer : UserControl
    {
        private readonly ISystemMonitorService _systemMonitorService;
        private readonly DispatcherTimer _metricsTimer;
        private readonly DispatcherTimer _vitalsPollTimer;
        private readonly InstrumentStackDrawerViewModel _viewModel;
        private DispatcherTimer? _singleClickTimer;
        private bool _metricsUpdateInProgress;
        private bool _vitalsPollInProgress;
        private bool _isUnloaded;

        public InstrumentStackDrawer()
        {
            InitializeComponent();

            try
            {
                _systemMonitorService = App.GetService<ISystemMonitorService>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get ISystemMonitorService: {ex.Message}");
                _systemMonitorService = new HouseVictoria.Services.SystemMonitor.SystemMonitorService();
            }

            _viewModel = new InstrumentStackDrawerViewModel(_systemMonitorService, DrawerPanel, VitalsDrawerStub);
            DataContext = _viewModel;

            _viewModel.System.PropertyChanged += System_PropertyChanged;

            _metricsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            _metricsTimer.Tick += MetricsTimer_Tick;
            _metricsTimer.Start();

            _vitalsPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _vitalsPollTimer.Tick += VitalsPollTimer_Tick;
            _vitalsPollTimer.Start();
        }

        private void System_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemMonitorDrawerViewModel.SelectedTabIndex))
            {
                if (_viewModel.System.SelectedTabIndex <= InstrumentStackDrawerViewModel.ControlTabIndex)
                    _viewModel.Vitals.SelectedTabIndex = _viewModel.System.SelectedTabIndex;
            }
            else if (e.PropertyName == nameof(SystemMonitorDrawerViewModel.IsDrawerOpen)
                     && !_viewModel.System.IsDrawerOpen
                     && _viewModel.Vitals.CollapseState == VitalsDrawerCollapseState.Open)
            {
                _viewModel.Vitals.CollapseState = VitalsDrawerCollapseState.Pulse;
            }
        }

        private async void MetricsTimer_Tick(object? sender, EventArgs e)
        {
            if (_isUnloaded || _metricsUpdateInProgress)
                return;

            _metricsUpdateInProgress = true;
            try
            {
                await System.Threading.Tasks.Task.Run(() => _viewModel.System.UpdateMetrics()).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InstrumentStack metrics error: {ex.Message}");
            }
            finally
            {
                _metricsUpdateInProgress = false;
            }
        }

        private async void VitalsPollTimer_Tick(object? sender, EventArgs e)
        {
            if (_isUnloaded || _vitalsPollInProgress)
                return;

            _vitalsPollInProgress = true;
            try
            {
                await _viewModel.Vitals.PollTradingVitalsAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InstrumentStack vitals poll error: {ex.Message}");
            }
            finally
            {
                _vitalsPollInProgress = false;
            }
        }

        private void Control_Unloaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = true;
            _metricsTimer.Stop();
            _vitalsPollTimer.Stop();
            _singleClickTimer?.Stop();
            _viewModel.System.PropertyChanged -= System_PropertyChanged;
            _viewModel.Dispose();
        }

        private void PulseWidget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                CancelPendingSingleClick();
                _viewModel.CollapseToHandle();
                e.Handled = true;
                return;
            }

            if (e.ClickCount == 1)
                ScheduleSingleClickOpen();
        }

        private void DrawerHeaderPulse_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _viewModel.CollapseDrawerToPulse();
            e.Handled = true;
        }

        private void ScheduleSingleClickOpen()
        {
            CancelPendingSingleClick();
            _singleClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
            _singleClickTimer.Tick += (_, _) =>
            {
                CancelPendingSingleClick();
                _viewModel.OpenVitalsTab();
            };
            _singleClickTimer.Start();
        }

        private void CancelPendingSingleClick()
        {
            if (_singleClickTimer == null)
                return;

            _singleClickTimer.Stop();
            _singleClickTimer = null;
        }

        private void RootGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var source = e.OriginalSource as DependencyObject;
                if (source != null && (IsDescendantOf(source, CollapsedPullHandle)
                    || IsDescendantOf(source, PulseWidget)
                    || IsDescendantOf(source, PullTabHost)
                    || IsDescendantOf(source, DrawerPanel)))
                    return;

                if (_viewModel.System.IsDrawerOpen)
                    _viewModel.CollapseDrawerToPulse();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InstrumentStack mouse down: {ex.Message}");
            }
        }

        private static bool IsDescendantOf(DependencyObject child, DependencyObject ancestor)
        {
            if (ancestor == null)
                return false;

            var current = child;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;

                current = current is Visual or System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
            }

            return false;
        }
    }
}
