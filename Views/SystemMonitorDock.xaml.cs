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
            // Height 25% of screen
            double h = SystemParameters.PrimaryScreenHeight * 0.25;
            this.Height = h;

            // Snap Bottom Left
            this.Top = SystemParameters.PrimaryScreenHeight - h;
            this.Left = 0;
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_monWindow == null || !_monWindow.IsVisible)
            {
                _monWindow = new SystemMonitorWindow();
                _monWindow.Show();
            }
            else
            {
                var vm = _monWindow.DataContext as SystemMonitorViewModel;
                vm?.ToggleViewCommand.Execute(null);
            }
        }
    }
}