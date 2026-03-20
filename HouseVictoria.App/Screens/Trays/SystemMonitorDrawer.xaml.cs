using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using System.Windows.Media.Media3D;

namespace HouseVictoria.App.Screens.Trays
{
    public partial class SystemMonitorDrawer : UserControl
    {
        private readonly ISystemMonitorService _systemMonitorService;
        private readonly DispatcherTimer _updateTimer;
        private readonly SystemMonitorDrawerViewModel _viewModel;

        public SystemMonitorDrawer()
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
            _viewModel = new SystemMonitorDrawerViewModel(_systemMonitorService, DrawerPanel);
            DataContext = _viewModel;

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) // Reduced frequency to avoid UI blocking
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            _viewModel.UpdateMetrics();
        }

        private void Control_Unloaded(object sender, RoutedEventArgs e)
        {
            _updateTimer.Stop();
            _viewModel?.Dispose();
        }

        private void RootGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // If the click is inside the pull tab or inside the drawer itself, do nothing
                var source = e.OriginalSource as DependencyObject;
                if (source != null && (IsDescendantOf(source, CollapsedPullHandle) || IsDescendantOf(source, PullTabHost) || IsDescendantOf(source, DrawerPanel)))
                    return;

                // Clicked outside the drawer area – collapse it
                _viewModel.IsDrawerOpen = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling RootGrid_MouseDown: {ex.Message}");
            }
        }

        private static bool IsDescendantOf(DependencyObject child, DependencyObject ancestor)
        {
            if (ancestor == null) return false;

            DependencyObject current = child;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;

                // Handle both Visual and Visual3D trees (WPF can mix them)
                if (current is Visual || current is Visual3D)
                    current = VisualTreeHelper.GetParent(current);
                else
                    current = LogicalTreeHelper.GetParent(current);
            }

            return false;
        }
    }
}

