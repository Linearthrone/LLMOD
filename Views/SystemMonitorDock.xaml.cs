using System.Windows;
using LLMOD.ViewModels;

namespace LLMOD.Views
{
    public partial class SystemMonitorDock : Window
    {
        private SystemMonitorWindow? _monWindow;

        public SystemMonitorDock()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position just above the Windows taskbar at bottom-left
            this.Left = 0;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 40;

            // Don't auto-open the monitor window - it's now a toggle button
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleMonitor();
        }

        private void ToggleMonitor()
        {
            if (_monWindow == null || !_monWindow.IsVisible)
            {
                _monWindow = new SystemMonitorWindow();
                _monWindow.Show();
            }
            else
            {
                _monWindow.Close();
                _monWindow = null;
            }
        }
    }
}