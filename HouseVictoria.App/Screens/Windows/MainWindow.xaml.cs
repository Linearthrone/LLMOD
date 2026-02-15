using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Utils;
using static HouseVictoria.Core.Utils.LoggingHelper;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class MainWindow : Window
    {
        private readonly IEventAggregator _eventAggregator = null!; // Initialized in constructor
        private AppContext? _appContext;
        private AutoHideBehavior? _topTrayAutoHide;
        private bool _autoHideEnabled = false;
        private TaskbarIcon? _notifyIcon;
        private SMSMMSWindow? _smsWindow;
        private AIModelsWindow? _aiModelsWindow;
        private SettingsWindow? _settingsWindow;
        private ProjectsWindow? _projectsWindow;
        private GlobalLogDirectoryWindow? _gldWindow;
        private DataBankManagementWindow? _dataBankManagementWindow;
        private VirtualEnvironmentControlsWindow? _virtualEnvironmentControlsWindow;
        private VideoCallWindow? _videoCallWindow;

        public MainWindow()
        {
            try
            {
                WriteToStartupLog("MainWindow constructor starting...");
                System.Diagnostics.Debug.WriteLine("MainWindow constructor starting...");
                
                InitializeComponent();
                WriteToStartupLog("MainWindow InitializeComponent completed");
                System.Diagnostics.Debug.WriteLine("MainWindow InitializeComponent completed");
                
                try
                {
                    _eventAggregator = App.GetService<IEventAggregator>();
                    WriteToStartupLog("IEventAggregator retrieved successfully");
                    System.Diagnostics.Debug.WriteLine("IEventAggregator retrieved successfully");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to get IEventAggregator: {ex.Message}\nStack: {ex.StackTrace}";
                    WriteToStartupLog(errorMsg);
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    _eventAggregator = new EventAggregator();
                    WriteToStartupLog("Using fallback EventAggregator");
                    System.Diagnostics.Debug.WriteLine("Using fallback EventAggregator");
                }
                
                // Ensure window is visible - CRITICAL FIX
                this.Visibility = Visibility.Visible;
                this.Show();
                this.Activate();
                
                var visibilityMsg = $"MainWindow created - Visibility: {this.Visibility}, WindowState: {this.WindowState}, IsVisible: {this.IsVisible}";
                WriteToStartupLog(visibilityMsg);
                System.Diagnostics.Debug.WriteLine(visibilityMsg);
            }
            catch (Exception ex)
            {
                var errorMsg = $"CRITICAL MainWindow Constructor Error: {ex.Message}\n{ex.StackTrace}";
                System.Diagnostics.Debug.WriteLine(errorMsg);
                WriteToStartupLog(errorMsg);
                MessageBox.Show($"MainWindow Constructor Error: {ex.Message}\n\n{ex.StackTrace}", "Constructor Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw; // Re-throw to prevent silent failure
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Setup window message hook for click-through handling as soon as handle is available
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null)
            {
                source.AddHook(WndProc);
                System.Diagnostics.Debug.WriteLine("MainWindow: WndProc hook installed in OnSourceInitialized");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow Window_Loaded event fired");
                _appContext = new AppContext();
                DataContext = _appContext;

                _eventAggregator.Subscribe<ShowWindowEvent>(OnShowWindow);
                _eventAggregator.Subscribe<HideWindowEvent>(OnHideWindow);
                _eventAggregator.Subscribe<ToggleTrayEvent>(OnToggleTray);

                // Create system tray icon
                SetupSystemTrayIcon();

                // Setup auto-hide for both trays
                SetupAutoHide();
                
                // Ensure window is visible - CRITICAL FIX
                this.Visibility = Visibility.Visible;
                this.Show();
                this.Activate();
                this.BringIntoView();
                
                System.Diagnostics.Debug.WriteLine("MainWindow Window_Loaded completed successfully");
                System.Diagnostics.Debug.WriteLine($"Window bounds: {this.Left},{this.Top},{this.Width},{this.Height}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in Window_Loaded: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error loading window: {ex.Message}", "Window Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private System.Drawing.Icon? CreateIconFromPngSource(BitmapSource bitmapSource)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Creating icon from PNG, SourceSize: {bitmapSource.PixelWidth}x{bitmapSource.PixelHeight}");

                // Create a bitmap from the source
                var bitmap = new System.Drawing.Bitmap(bitmapSource.PixelWidth, bitmapSource.PixelHeight);

                // Copy pixels from BitmapSource to Bitmap
                var stride = bitmapSource.PixelWidth * ((bitmapSource.Format.BitsPerPixel + 7) / 8);
                var pixels = new byte[stride * bitmapSource.PixelHeight];
                bitmapSource.CopyPixels(pixels, stride, 0);

                // Lock bitmap data for fast pixel access
                var bmpData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                // Copy the pixels to the bitmap data
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
                bitmap.UnlockBits(bmpData);

                // Create icon from bitmap
                var iconHandle = bitmap.GetHicon();
                var icon = System.Drawing.Icon.FromHandle(iconHandle);

                // Create a copy and return it (to avoid issues with the original bitmap)
                System.Diagnostics.Debug.WriteLine("Icon created successfully from PNG");
                return (System.Drawing.Icon)icon.Clone();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create icon from PNG: {ex.Message}");
                return null;
            }
        }

        private void SetupSystemTrayIcon()
        {
            try
            {
                _notifyIcon = new TaskbarIcon
                {
                    ToolTipText = "House Victoria - Double-click to toggle auto-hide",
                };

                bool iconLoaded = false;

                // Try to load PNG and convert to icon
                try
                {
                    var pngUri = new Uri("pack://application:,,,/HouseVictoria.App;component/Resources/SYSICO.png", UriKind.Absolute);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = pngUri;
                    bitmapImage.DecodePixelWidth = 32;
                    bitmapImage.DecodePixelHeight = 32;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    var icon = CreateIconFromPngSource(bitmapImage);
                    if (icon != null)
                    {
                        _notifyIcon.Icon = icon;
                        iconLoaded = true;
                        System.Diagnostics.Debug.WriteLine("Icon loaded from PNG successfully");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"PNG icon loading failed: {ex.Message}");
                }

                // Fallback methods if PNG fails
                if (!iconLoaded)
                {
                    try
                    {
                        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        if (System.IO.File.Exists(exePath))
                        {
                            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                            iconLoaded = true;
                            System.Diagnostics.Debug.WriteLine("Icon loaded from executable");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Executable icon failed: {ex.Message}");
                    }
                }

                if (!iconLoaded)
                {
                    try
                    {
                        _notifyIcon.Icon = System.Drawing.SystemIcons.Information;
                        iconLoaded = true;
                        System.Diagnostics.Debug.WriteLine("Using SystemIcons.Information");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"SystemIcons.Information failed: {ex.Message}");
                    }
                }

                if (!iconLoaded)
                {
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                    System.Diagnostics.Debug.WriteLine("Using default SystemIcons.Application");
                }

                // Create context menu
                var contextMenu = new System.Windows.Controls.ContextMenu();
                var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
                settingsItem.Click += MenuItem_Settings_Click;

                var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
                exitItem.Click += MenuItem_Exit_Click;

                contextMenu.Items.Add(settingsItem);
                contextMenu.Items.Add(new System.Windows.Controls.Separator());
                contextMenu.Items.Add(exitItem);

                _notifyIcon.ContextMenu = contextMenu;

                _notifyIcon.TrayMouseDoubleClick += NotifyIcon_TrayMouseDoubleClick;

                System.Diagnostics.Debug.WriteLine("System tray icon created successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up system tray icon: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void SetupAutoHide()
        {
            // Automatically enable auto-hide for top tray only
            _autoHideEnabled = true;

            // Get tray controls from the visual tree
            if (this.FindName("TopTrayElement") is FrameworkElement topTray)
            {
                _topTrayAutoHide = new AutoHideBehavior(topTray, 2500, -240, HideDirection.Top);
                _topTrayAutoHide.IsVisible = true; // Start visible
                System.Diagnostics.Debug.WriteLine($"TopTray auto-hide initialized");
            }

            // Main tray should NOT auto-hide - leave it always visible
            // Removed MainTray auto-hide setup
        }

        private void PullHandle_MouseEnter(object sender, MouseEventArgs e)
        {
            var handle = (Border)sender;
            handle.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 0, 212, 255));
            handle.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 0, 212, 255));
            if (handle.Child is TextBlock arrow)
            {
                arrow.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 212, 255));
            }
        }

        private void PullHandle_MouseLeave(object sender, MouseEventArgs e)
        {
            var handle = (Border)sender;
            handle.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(32, 0, 212, 255));
            handle.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 0, 212, 255));
            if (handle.Child is TextBlock arrow)
            {
                arrow.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 0, 212, 255));
            }
        }

        private void PullHandle_Click(object sender, MouseButtonEventArgs e)
        {
            // Toggle top tray visibility
            if (_topTrayAutoHide != null)
            {
                _topTrayAutoHide.IsVisible = !_topTrayAutoHide.IsVisible;
            }
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            // Remove window message hook
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null)
            {
                source.RemoveHook(WndProc);
            }
            
            _eventAggregator.Unsubscribe<ShowWindowEvent>(OnShowWindow);
            _eventAggregator.Unsubscribe<HideWindowEvent>(OnHideWindow);
            _eventAggregator.Unsubscribe<ToggleTrayEvent>(OnToggleTray);
            _appContext?.Dispose();
            _topTrayAutoHide?.Dispose();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visibility = System.Windows.Visibility.Hidden;
                _notifyIcon.Dispose();
            }
        }

        #region Click-Through Handling

        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;
        private const int HTCLIENT = 1;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                // Get screen coordinates
                var x = (int)(lParam.ToInt64() & 0xFFFF);
                var y = (int)((lParam.ToInt64() >> 16) & 0xFFFF);
                var screenPoint = new Point(x, y);

                try
                {
                    // Convert to window coordinates
                    var point = PointFromScreen(screenPoint);

                    // Use InputHitTest to find what element was hit
                    var hitElement = InputHitTest(point);
                    
                    // Helper function to check if an element is within one of our interactive elements
                    bool IsWithinInteractiveElement(DependencyObject element)
                    {
                        var current = element;
                        while (current != null && current != this)
                        {
                            if (current == PullHandle ||
                                current == MainTrayElement ||
                                current == TopTrayElement ||
                                current == SystemMonitorDrawerElement ||
                                current == WindowContainer)
                            {
                                return true;
                            }
                            
                            var parent = VisualTreeHelper.GetParent(current);
                            if (parent == null)
                            {
                                parent = LogicalTreeHelper.GetParent(current);
                            }
                            current = parent;
                        }
                        return false;
                    }
                    
                    // If we hit something, check if it's within an interactive element
                    if (hitElement != null && hitElement is DependencyObject hitObj)
                    {
                        // Special case: if we hit the Window or Grid (Content) directly, allow click-through
                        // unless we're actually within an interactive element
                        if (hitObj == this || hitObj == Content)
                        {
                            // Hit the window/grid directly - allow click-through (empty space)
                            handled = true;
                            return new IntPtr(HTTRANSPARENT);
                        }
                        
                        // Check if the hit element is within any interactive element
                        if (IsWithinInteractiveElement(hitObj))
                        {
                            // Found an interactive element - let WPF handle the click normally
                            handled = false;
                            return IntPtr.Zero;
                        }
                    }

                    // If we didn't find any interactive element, allow click-through
                    handled = true;
                    return new IntPtr(HTTRANSPARENT);
                }
                catch (Exception ex)
                {
                    // On error, log and default to allowing click-through for safety
                    System.Diagnostics.Debug.WriteLine($"Error in WndProc hit testing: {ex.Message}");
                    handled = true;
                    return new IntPtr(HTTRANSPARENT);
                }
            }

            return IntPtr.Zero;
        }

        #endregion

        private void NotifyIcon_TrayMouseDoubleClick(object sender, EventArgs e)
        {
            // Toggle auto-hide on double-click
            if (_autoHideEnabled)
            {
                // Disable auto-hide (top tray only)
                _topTrayAutoHide?.Dispose();
                _topTrayAutoHide = null;
                _autoHideEnabled = false;

                // Reset top tray position
                if (this.FindName("TopTrayElement") is FrameworkElement topTray)
                {
                    topTray.Margin = new Thickness(topTray.Margin.Left, 10, topTray.Margin.Right, topTray.Margin.Bottom);
                    topTray.Opacity = 1;
                }
            }
            else
            {
                // Enable auto-hide (top tray only)
                _autoHideEnabled = true;
                SetupAutoHide();
            }
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuItem_Settings_Click(object sender, RoutedEventArgs e)
        {
            // Open Settings window
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.Show();
        }

        private void OnShowWindow(ShowWindowEvent evt)
        {
            try
            {
                Window? windowToOpen = null;
                
                if (evt.WindowType == "SMS/MMS")
                {
                    // Check if SMS window already exists
                    if (_smsWindow != null && !_smsWindow.IsClosed())
                    {
                        // Window exists, restore it if minimized
                        if (_smsWindow.IsMinimized())
                        {
                            _smsWindow.RestoreFromMinimized();
                        }
                        _smsWindow.Activate();
                        _smsWindow.Focus();
                        return;
                    }
                    else
                    {
                        // Create new window
                        _smsWindow = new SMSMMSWindow();
                        windowToOpen = _smsWindow;
                        _smsWindow.Closed += (s, e) => { _smsWindow = null; };
                    }
                }
                else if (evt.WindowType == "AIModels")
                {
                    if (_aiModelsWindow != null && !_aiModelsWindow.IsClosed())
                    {
                        if (_aiModelsWindow.IsMinimized())
                        {
                            _aiModelsWindow.RestoreFromMinimized();
                        }
                        _aiModelsWindow.Activate();
                        _aiModelsWindow.Focus();
                        return;
                    }
                    else
                    {
                        _aiModelsWindow = new AIModelsWindow();
                        windowToOpen = _aiModelsWindow;
                        _aiModelsWindow.Closed += (s, e) => { _aiModelsWindow = null; };
                    }
                }
                else if (evt.WindowType == "Settings")
                {
                    if (_settingsWindow != null && !_settingsWindow.IsClosed())
                    {
                        if (_settingsWindow.IsMinimized())
                        {
                            _settingsWindow.RestoreFromMinimized();
                        }
                        _settingsWindow.Activate();
                        _settingsWindow.Focus();
                        return;
                    }
                    else
                    {
                        _settingsWindow = new SettingsWindow();
                        windowToOpen = _settingsWindow;
                        _settingsWindow.Closed += (s, e) => { _settingsWindow = null; };
                    }
                }
                else if (evt.WindowType == "Projects")
                {
                    if (_projectsWindow != null && !_projectsWindow.IsClosed())
                    {
                        if (_projectsWindow.IsMinimized())
                        {
                            _projectsWindow.RestoreFromMinimized();
                        }
                        _projectsWindow.Activate();
                        _projectsWindow.Focus();
                        return;
                    }
                    else
                    {
                        _projectsWindow = new ProjectsWindow();
                        windowToOpen = _projectsWindow;
                        _projectsWindow.Closed += (s, e) => { _projectsWindow = null; };
                    }
                }
                else if (evt.WindowType == "GLD")
                {
                    if (_gldWindow != null && !_gldWindow.IsClosed())
                    {
                        if (_gldWindow.IsMinimized())
                        {
                            _gldWindow.RestoreFromMinimized();
                        }
                        _gldWindow.Activate();
                        _gldWindow.Focus();
                        return;
                    }
                    else
                    {
                        _gldWindow = new GlobalLogDirectoryWindow();
                        windowToOpen = _gldWindow;
                        _gldWindow.Closed += (s, e) => { _gldWindow = null; };
                    }
                }
                else if (evt.WindowType == "DataBankManagement")
                {
                    if (_dataBankManagementWindow != null && !_dataBankManagementWindow.IsClosed())
                    {
                        if (_dataBankManagementWindow.IsMinimized())
                        {
                            _dataBankManagementWindow.RestoreFromMinimized();
                        }
                        _dataBankManagementWindow.Activate();
                        _dataBankManagementWindow.Focus();
                        return;
                    }
                    else
                    {
                        _dataBankManagementWindow = new DataBankManagementWindow();
                        windowToOpen = _dataBankManagementWindow;
                        _dataBankManagementWindow.Closed += (s, e) => { _dataBankManagementWindow = null; };
                    }
                }
                else if (evt.WindowType == "VirtualEnvironmentControls")
                {
                    if (_virtualEnvironmentControlsWindow != null && !_virtualEnvironmentControlsWindow.IsClosed())
                    {
                        if (_virtualEnvironmentControlsWindow.IsMinimized())
                        {
                            _virtualEnvironmentControlsWindow.RestoreFromMinimized();
                        }
                        _virtualEnvironmentControlsWindow.Activate();
                        _virtualEnvironmentControlsWindow.Focus();
                        return;
                    }
                    else
                    {
                        _virtualEnvironmentControlsWindow = new VirtualEnvironmentControlsWindow();
                        windowToOpen = _virtualEnvironmentControlsWindow;
                        _virtualEnvironmentControlsWindow.Closed += (s, e) => { _virtualEnvironmentControlsWindow = null; };
                    }
                }
                else if (evt.WindowType == "VideoCall")
                {
                    var callContext = evt.Data as VideoCallContext;
                    if (_videoCallWindow != null && !_videoCallWindow.IsClosed())
                    {
                        _videoCallWindow.UpdateContext(callContext);
                        if (_videoCallWindow.IsMinimized())
                        {
                            _videoCallWindow.RestoreFromMinimized();
                        }
                        _videoCallWindow.Activate();
                        _videoCallWindow.Focus();
                        return;
                    }
                    else
                    {
                        _videoCallWindow = new VideoCallWindow(callContext);
                        windowToOpen = _videoCallWindow;
                        _videoCallWindow.Closed += (s, e) => { _videoCallWindow = null; };
                    }
                }

            if (windowToOpen != null)
            {
                windowToOpen.Owner = this;
                windowToOpen.Show();
                    windowToOpen.Activate();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"OnShowWindow: Unknown window type: {evt.WindowType}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening window '{evt.WindowType}': {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error opening window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnHideWindow(HideWindowEvent evt)
        {
            // WindowContainer.Visibility = Visibility.Collapsed;
        }

        private void OnToggleTray(ToggleTrayEvent evt)
        {
            try
            {
                if (evt.TrayName == "MainTray")
                {
                    if (this.FindName("MainTrayElement") is FrameworkElement mainTray)
                    {
                        // Toggle visibility with smooth fade animation
                        if (mainTray.Visibility == Visibility.Visible || mainTray.Opacity > 0.5)
                        {
                            // Hide the tray with fade animation
                            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
                            {
                                From = mainTray.Opacity,
                                To = 0.0,
                                Duration = TimeSpan.FromMilliseconds(200)
                            };
                            fadeOut.Completed += (s, e) =>
                            {
                                mainTray.Visibility = Visibility.Collapsed;
                            };
                            mainTray.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                            System.Diagnostics.Debug.WriteLine("MainTray: Hidden with fade animation");
                        }
                        else
                        {
                            // Show the tray with fade animation
                            mainTray.Visibility = Visibility.Visible;
                            mainTray.Opacity = 0.0;
                            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
                            {
                                From = 0.0,
                                To = 1.0,
                                Duration = TimeSpan.FromMilliseconds(200)
                            };
                            mainTray.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                            System.Diagnostics.Debug.WriteLine("MainTray: Shown with fade animation");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling tray '{evt.TrayName}': {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    public class AppContext : ObservableObject
    {
        private string _systemUptime = "00:00:00";

        public string SystemUptime
        {
            get => _systemUptime;
            set => SetProperty(ref _systemUptime, value);
        }

        public void UpdateSystemUptime(TimeSpan uptime)
        {
            SystemUptime = $"{uptime.Days}d {uptime.Hours:00}h {uptime.Minutes:00}m {uptime.Seconds:00}s";
        }

        public void Dispose()
        {
        }
    }

    public class ShowWindowEvent
    {
        public string WindowType { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    public class HideWindowEvent
    {
        public string? WindowType { get; set; }
    }

    public class ToggleTrayEvent
    {
        public string TrayName { get; set; } = string.Empty;
    }
}
