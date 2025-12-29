using System.Windows;
using LLMOD.Views; // <--- CRITICAL: You must have this line

namespace LLMOD
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position the Main LLMOD Tray (Right Side)
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double trayWidth = 70;
            double trayHeight = screenHeight * 0.50;

            this.Width = trayWidth;
            this.Height = trayHeight;
            this.Left = screenWidth - trayWidth;
            this.Top = (screenHeight - trayHeight) / 2.0;
        }

        private void OpenChat_Click(object sender, RoutedEventArgs e)
        {
            var chat = new ChatWindow();
            chat.Show();
        }

        private void OpenSetup_Click(object sender, RoutedEventArgs e)
        {
            var config = new ProcessConfigWindow();
            config.Show();
        }

        // --- THIS OPENS THE SYSTEM MONITOR ---
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var sysDock = new SystemMonitorDock();
            sysDock.Show();
        }
    }
}