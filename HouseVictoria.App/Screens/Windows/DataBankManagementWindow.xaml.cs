using System;
using System.Windows;
using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class DataBankManagementWindow : Window
    {
        private readonly IMemoryService _memoryService;

        public DataBankManagementWindowViewModel ViewModel { get; }
        
        private bool _isMinimized = false;
        private bool _isClosed = false;

        public DataBankManagementWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing DataBankManagementWindow XAML: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error initializing Data Bank Management Window: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            
            try
            {
                _memoryService = App.GetService<IMemoryService>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get IMemoryService: {ex.Message}");
                MessageBox.Show($"Error: Could not access Memory Service. {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            
            try
            {
                ViewModel = new DataBankManagementWindowViewModel(_memoryService);
                DataContext = ViewModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating DataBankManagementWindowViewModel: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error initializing view model: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }

            Loaded += DataBankManagementWindow_Loaded;
        }

        private void DataBankManagementWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure window fits on screen (preserves XAML sizes, adjusts if off-screen or too large)
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    WindowHelper.EnsureWindowFitsOnScreen(this);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error fitting DataBankManagementWindow on screen: {ex.Message}");
                }
            }));
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

        private async void DataBankCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string bankId)
            {
                await ViewModel.SelectDataBankAsync(bankId);
            }
        }
    }
}
