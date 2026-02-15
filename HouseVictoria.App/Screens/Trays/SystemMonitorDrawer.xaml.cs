using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;

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
    }
}

