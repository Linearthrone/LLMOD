using System.Windows;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsWindowViewModel? _viewModel;
        
        private bool _isMinimized = false;
        private bool _isClosed = false;
        private double _savedWidth;
        private double _savedHeight;
        private double _savedLeft;
        private double _savedTop;

        public SettingsWindow()
        {
            InitializeComponent();
            
            // Get AppConfig from service provider
            var appConfig = App.ServiceProvider?.GetService<AppConfig>();
            
            if (appConfig != null)
            {
                _viewModel = new SettingsWindowViewModel(appConfig);
                DataContext = _viewModel;
            }
            else
            {
                // Fallback: create a default AppConfig if service provider is not available
                _viewModel = new SettingsWindowViewModel(new Core.Models.AppConfig());
                DataContext = _viewModel;
            }
            
            Loaded += SettingsWindow_Loaded;
            
            Closed += (s, e) => { _isClosed = true; };
        }

        public bool IsClosed() => _isClosed;
        public bool IsMinimized() => _isMinimized;
        
        public void RestoreFromMinimized()
        {
            WindowHelper.RestoreFromTray(this, ref _isMinimized, _savedWidth, _savedHeight, _savedLeft, _savedTop);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowHelper.MinimizeToTray(this, ref _isMinimized, ref _savedWidth, ref _savedHeight, ref _savedLeft, ref _savedTop);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.SaveSettings();
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _savedWidth = Width;
            _savedHeight = Height;
            _savedLeft = Left;
            _savedTop = Top;

            // Ensure window fits on screen (preserves XAML sizes, adjusts if off-screen or too large)
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    WindowHelper.EnsureWindowFitsOnScreen(this);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error fitting SettingsWindow on screen: {ex.Message}");
                }
            }));
        }
    }
}
