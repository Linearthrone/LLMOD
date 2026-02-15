using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.App.Converters;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class SMSMMSWindow : Window
    {
        private readonly ICommunicationService _communicationService;

        public SMSMMSWindowViewModel ViewModel { get; }

        public SMSMMSWindow()
        {
            InitializeComponent();
            try
            {
                _communicationService = App.GetService<ICommunicationService>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get ICommunicationService: {ex.Message}");
                _communicationService = new HouseVictoria.Services.Communication.SMSMMSCommunicationService();
            }
            IEventAggregator? eventAggregator = null;
            try
            {
                eventAggregator = App.GetService<IEventAggregator>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get IEventAggregator: {ex.Message}");
            }
            ViewModel = new SMSMMSWindowViewModel(_communicationService, eventAggregator);
            DataContext = ViewModel;

            ViewModel.Messages.CollectionChanged += Messages_CollectionChanged;

            Loaded += SMSMMSWindow_Loaded;
            Closed += SMSMMSWindow_Closed;
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Scroll to bottom after layout so the most recent messages are visible when opening a chat
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                MessagesScrollViewer?.ScrollToEnd();
            });
        }

        private void SMSMMSWindow_Closed(object? sender, EventArgs e)
        {
            if (ViewModel != null)
                ViewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
            _isClosed = true;
        }

        public bool IsClosed()
        {
            return _isClosed;
        }

        public bool IsMinimized()
        {
            return _isMinimized;
        }

        public void RestoreFromMinimized()
        {
            if (_isMinimized)
            {
                this.Width = _savedWidth;
                this.Height = _savedHeight;
                this.Left = _savedLeft;
                this.Top = _savedTop;
                this.Visibility = Visibility.Visible;
                this.Show();
                this.Activate();
                _isMinimized = false;
            }
        }

        private void SMSMMSWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Calculate window size and position
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            // Galaxy S23 actual dimensions: 146.3mm x 70.9mm = 2.064:1 aspect ratio
            // For a reasonable on-screen size, scale to about 400-450px width
            // This gives us a phone-like window that's usable
            const double sizeScaleFactor = 2.25; // Factor to increase initial window size
            var baseWidth = 420.0 * sizeScaleFactor; // Base width in pixels
            var aspectRatio = 146.3 / 70.9; // Actual S23 aspect ratio
            var windowWidth = baseWidth;
            var windowHeight = windowWidth / aspectRatio; // Maintain S23 proportions
            
            // MainTray is 90px wide + 20px margin = 110px from right edge
            var trayWidth = 110;
            
            // Position on right side, but to the left of the tray
            // Aligned more to the top (about 10% from top)
            var left = screenWidth - windowWidth - trayWidth - 20; // 20px gap between window and tray
            var top = screenHeight * 0.1; // 10% from top
            
            this.Width = windowWidth;
            this.Height = windowHeight;
            this.Left = left;
            this.Top = top;
            
            // Make window resizable with proper min/max constraints
            this.ResizeMode = ResizeMode.CanResize;
            this.MinWidth = 350;
            this.MinHeight = 150;
            this.MaxWidth = screenWidth * 0.6; // Max 60% of screen width
            this.MaxHeight = screenHeight * 0.9; // Max 90% of screen height

            // Ensure fully visible on screen (accounts for taskbar, multi-monitor)
            WindowHelper.EnsureWindowFitsOnScreen(this);
        }


        private void MediaPreview_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ConversationMessage? message = null;
            
            // Get message from DataContext (works for Image, TextBlock, or Border)
            if (sender is FrameworkElement element && element.DataContext is ConversationMessage msg)
            {
                message = msg;
            }
            
            if (message != null && !string.IsNullOrWhiteSpace(message.FilePath) && System.IO.File.Exists(message.FilePath))
            {
                try
                {
                    // Open file with default application
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = message.FilePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Diagnostics.Debug.WriteLine($"Error opening media file: {ex.Message}");
                }
            }
        }


        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                if (ViewModel.SendMessageCommand.CanExecute(null))
                {
                    ViewModel.SendMessageCommand.Execute(null);
                }
                e.Handled = true;
            }
        }

        private void MessageTextBox_OnPaste(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is TextBox textBox && Clipboard.ContainsText())
            {
                textBox.SelectedText = Clipboard.GetText();
            }
            e.Handled = true;
        }

        private void MessageTextBox_OnPasteCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Clipboard.ContainsText();
            e.Handled = true;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate back in conversation or to conversation list
            if (!ViewModel.ShowConversationList)
            {
                ViewModel.BackToConversationListCommand.Execute(null);
            }
        }

        private bool _isMinimized = false;
        private bool _isClosed = false;
        private double _savedWidth;
        private double _savedHeight;
        private double _savedLeft;
        private double _savedTop;

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMinimized)
            {
                // Save current state
                _savedWidth = this.Width;
                _savedHeight = this.Height;
                _savedLeft = this.Left;
                _savedTop = this.Top;
                
                // Hide window completely (don't show on screen at all)
                this.Visibility = Visibility.Hidden;
                _isMinimized = true;
            }
            else
            {
                // Restore from minimized state
                this.Width = _savedWidth;
                this.Height = _savedHeight;
                this.Left = _savedLeft;
                this.Top = _savedTop;
                this.Visibility = Visibility.Visible;
                _isMinimized = false;
            }
        }

        private void RecentAppsButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleAppsViewCommand.Execute(null);
        }


        // P/Invoke for window resizing
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCHITTEST = 0x0084;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null)
            {
                source.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                // Only allow resize if not minimized and window is loaded
                if (_isMinimized || !IsLoaded)
                {
                    return IntPtr.Zero;
                }

                // Resize border thickness (the transparent border around the phone frame)
                var borderThickness = 8;
                
                // Get screen coordinates
                var x = (int)(lParam.ToInt64() & 0xFFFF);
                var y = (int)((lParam.ToInt64() >> 16) & 0xFFFF);
                var screenPoint = new System.Windows.Point(x, y);
                
                try
                {
                    // Convert to window coordinates
                    var point = PointFromScreen(screenPoint);
                    
                    var width = this.ActualWidth;
                    var height = this.ActualHeight;

                    // Validate dimensions
                    if (width <= 0 || height <= 0 || double.IsNaN(width) || double.IsNaN(height))
                    {
                        return IntPtr.Zero;
                    }

                    // Top-left corner
                    if (point.X < borderThickness && point.Y < borderThickness)
                    {
                        handled = true;
                        return new IntPtr(HTTOPLEFT);
                    }
                    // Top-right corner
                    if (point.X >= width - borderThickness && point.Y < borderThickness)
                    {
                        handled = true;
                        return new IntPtr(HTTOPRIGHT);
                    }
                    // Bottom-left corner
                    if (point.X < borderThickness && point.Y >= height - borderThickness)
                    {
                        handled = true;
                        return new IntPtr(HTBOTTOMLEFT);
                    }
                    // Bottom-right corner
                    if (point.X >= width - borderThickness && point.Y >= height - borderThickness)
                    {
                        handled = true;
                        return new IntPtr(HTBOTTOMRIGHT);
                    }
                    // Top edge
                    if (point.Y < borderThickness && point.X >= borderThickness && point.X <= width - borderThickness)
                    {
                        handled = true;
                        return new IntPtr(HTTOP);
                    }
                    // Bottom edge
                    if (point.Y >= height - borderThickness && point.X >= borderThickness && point.X <= width - borderThickness)
                    {
                        handled = true;
                        return new IntPtr(HTBOTTOM);
                    }
                    // Left edge
                    if (point.X < borderThickness && point.Y >= borderThickness && point.Y <= height - borderThickness)
                    {
                        handled = true;
                        return new IntPtr(HTLEFT);
                    }
                    // Right edge
                    if (point.X >= width - borderThickness && point.Y >= borderThickness && point.Y <= height - borderThickness)
                    {
                        handled = true;
                        return new IntPtr(HTRIGHT);
                    }
                }
                catch
                {
                    // If coordinate conversion fails, return default
                    return IntPtr.Zero;
                }
            }
            return IntPtr.Zero;
        }

        private void TopBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
