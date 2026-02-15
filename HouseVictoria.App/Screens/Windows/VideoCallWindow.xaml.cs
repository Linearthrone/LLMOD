using System;
using System.Windows;
using HouseVictoria.Core.Interfaces;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class VideoCallWindow : Window
    {
        private bool _isMinimized;
        private bool _isClosed;
        private readonly ICommunicationService _communicationService;

        public VideoCallWindowViewModel ViewModel { get; }

        public VideoCallWindow(VideoCallContext? context = null)
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing VideoCallWindow XAML: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error initializing Video Call Window: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }

            try
            {
                _communicationService = App.GetService<ICommunicationService>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get ICommunicationService: {ex.Message}");
                _communicationService = new HouseVictoria.Services.Communication.SMSMMSCommunicationService();
            }

            ViewModel = new VideoCallWindowViewModel(_communicationService, context);
            DataContext = ViewModel;
            Closed += VideoCallWindow_Closed;
        }

        private void VideoCallWindow_Closed(object? sender, EventArgs e)
        {
            _isClosed = true;
            ViewModel.Dispose();
        }

        public void UpdateContext(VideoCallContext? context)
        {
            ViewModel.UpdateContext(context);
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

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
