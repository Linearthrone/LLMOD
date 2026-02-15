using System;
using System.Windows;
using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class VirtualEnvironmentControlsWindow : Window
    {
        private readonly IVirtualEnvironmentService? _virtualEnvironmentService;
        public VirtualEnvironmentControlsWindowViewModel ViewModel { get; }
        
        private bool _isMinimized = false;
        private bool _isClosed = false;

        public VirtualEnvironmentControlsWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing VirtualEnvironmentControlsWindow XAML: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error initializing Virtual Environment Controls Window: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            
            try
            {
                _virtualEnvironmentService = App.GetService<IVirtualEnvironmentService>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get IVirtualEnvironmentService: {ex.Message}");
            }
            
            try
            {
                ViewModel = new VirtualEnvironmentControlsWindowViewModel(_virtualEnvironmentService);
                DataContext = ViewModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating VirtualEnvironmentControlsWindowViewModel: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error initializing view model: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
            _isMinimized = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _isClosed = true;
            Close();
        }

        public bool IsMinimized()
        {
            return _isMinimized || WindowState == WindowState.Minimized;
        }

        public bool IsClosed()
        {
            return _isClosed || !IsLoaded;
        }

        public void RestoreFromMinimized()
        {
            if (_isMinimized)
            {
                WindowState = WindowState.Normal;
                _isMinimized = false;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }

        private void AvatarCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is AvatarViewModel avatar)
            {
                ViewModel.SelectAvatar(avatar.Id);
            }
        }
    }
}
