using System.Windows; // Required for SystemParameters

namespace LLMOD.Views
{
    public partial class SystemMonitorWindow : Window
    {
        public Action? OnRequestClose { get; set; }

        public SystemMonitorWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Safe Positioning
            double screenH = 800; // Default hardcoded to prevent crash if SystemParameters fails on some setups
            try { screenH = SystemParameters.PrimaryScreenHeight; } catch { }

            this.Top = screenH - 800;
            this.Left = 0;

            // Optional: Force visible to ensure it's not just transparent
            // this.Width = 1100;
            // this.Height = 800;
        }
        private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            OnRequestClose?.Invoke();
        }    }
}