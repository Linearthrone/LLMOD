using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class GlobalLogDirectoryWindow : Window
    {
        public GlobalLogDirectoryWindowViewModel ViewModel { get; }

        private sealed class LinkedFileItem
        {
            public string Path { get; init; } = string.Empty;
            public string DisplayName { get; init; } = string.Empty;
        }

        private bool _isMinimized = false;
        private bool _isClosed = false;
        private double _savedWidth;
        private double _savedHeight;
        private double _savedLeft;
        private double _savedTop;

        public GlobalLogDirectoryWindow()
        {
            InitializeComponent();

            var loggingService = App.GetService<ILoggingService>();
            ViewModel = new GlobalLogDirectoryWindowViewModel(loggingService);
            DataContext = ViewModel;

            Loaded += GlobalLogDirectoryWindow_Loaded;
            SourceInitialized += (_, _) =>
            {
                if (PresentationSource.FromVisual(this) is HwndSource source)
                    source.AddHook(WndProc);
            };

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

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RefreshCommand.Execute(null);
        }

        private void MarkAllReadButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.MarkAllReadCommand.Execute(null);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ExportCommand.Execute(null);
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }

        private void LogTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Use FindName to locate controls in case they're not auto-generated
            var noSelectionHint = FindName("NoSelectionHint") as System.Windows.Controls.TextBlock;
            var logDetailsPanel = FindName("LogDetailsPanel") as System.Windows.Controls.Grid;
            var logTitle = FindName("LogTitle") as System.Windows.Controls.TextBlock;
            var logDateTime = FindName("LogDateTime") as System.Windows.Controls.TextBlock;
            var logSeverity = FindName("LogSeverity") as System.Windows.Controls.TextBlock;
            var logSource = FindName("LogSource") as System.Windows.Controls.TextBlock;
            var logSummary = FindName("LogSummary") as System.Windows.Controls.TextBlock;
            var logContent = FindName("LogContent") as System.Windows.Controls.TextBox;
            var logTags = FindName("LogTags") as System.Windows.Controls.ItemsControl;
            var linkedFilesPanel = FindName("LinkedFilesPanel") as System.Windows.Controls.Border;
            var logLinkedFiles = FindName("LogLinkedFiles") as System.Windows.Controls.ItemsControl;
            var imagePreviewPanel = FindName("ImagePreviewPanel") as System.Windows.Controls.Border;
            var imagePreviews = FindName("ImagePreviews") as System.Windows.Controls.ItemsControl;

            if (e.NewValue is LogCategoryViewModel selectedItem)
            {
                if (selectedItem.LogEntry != null)
                {
                    // This is a leaf node (actual log), show details
                    if (noSelectionHint != null) noSelectionHint.Visibility = Visibility.Collapsed;
                    if (logDetailsPanel != null) logDetailsPanel.Visibility = Visibility.Visible;

                    var entry = selectedItem.LogEntry;
                    ViewModel.NotifyLogEntrySelected(entry.Id);
                    ViewModel.SelectLogEntryAsync(entry).ConfigureAwait(false);

                    if (logTitle != null) logTitle.Text = entry.Title;
                    if (logDateTime != null) logDateTime.Text = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                    if (logSeverity != null) logSeverity.Text = entry.Severity.ToString();
                    if (logSource != null) logSource.Text = entry.Source;
                    if (logSummary != null) logSummary.Text = entry.Summary;
                    if (logContent != null) logContent.Text = WrapLongLines(entry.Content);

                    var linkedItems = entry.LinkedFilePaths
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(p => new LinkedFileItem
                        {
                            Path = p,
                            DisplayName = Path.GetFileName(p) + "  (" + p + ")"
                        })
                        .ToList();

                    if (linkedFilesPanel != null)
                        linkedFilesPanel.Visibility = linkedItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                    if (logLinkedFiles != null)
                        logLinkedFiles.ItemsSource = linkedItems;

                    var imageItems = linkedItems
                        .Where(i => IsImageFile(i.Path) && File.Exists(i.Path))
                        .ToList();

                    if (imagePreviews != null)
                        imagePreviews.ItemsSource = imageItems;

                    if (imagePreviewPanel != null)
                        imagePreviewPanel.Visibility = imageItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                    // Set tags
                    if (logTags != null)
                    {
                        logTags.Items.Clear();
                        foreach (var tag in entry.Tags)
                        {
                            logTags.Items.Add(tag);
                        }
                    }
                }
                else
                {
                    // This is a folder/parent node
                    if (noSelectionHint != null) noSelectionHint.Visibility = Visibility.Visible;
                    if (logDetailsPanel != null) logDetailsPanel.Visibility = Visibility.Collapsed;
                    if (linkedFilesPanel != null) linkedFilesPanel.Visibility = Visibility.Collapsed;
                    if (imagePreviewPanel != null) imagePreviewPanel.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Nothing selected
                if (noSelectionHint != null) noSelectionHint.Visibility = Visibility.Visible;
                if (logDetailsPanel != null) logDetailsPanel.Visibility = Visibility.Collapsed;
                if (linkedFilesPanel != null) linkedFilesPanel.Visibility = Visibility.Collapsed;
                if (imagePreviewPanel != null) imagePreviewPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void LinkedFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string path || string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if (!File.Exists(path))
                {
                    MessageBox.Show($"File not found:\n{path}", "Linked File", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open file:\n{path}\n\n{ex.Message}",
                    "Linked File",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ImagePreview_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Image image || image.Tag is not string path || string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if (!File.Exists(path))
                {
                    MessageBox.Show($"Image not found:\n{path}", "Image Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open image:\n{path}\n\n{ex.Message}",
                    "Image Preview",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static readonly string[] ImageExtensions =
            { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff" };

        private static bool IsImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var ext = Path.GetExtension(path);
            return !string.IsNullOrEmpty(ext) &&
                   ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private void GlobalLogDirectoryWindow_Loaded(object sender, RoutedEventArgs e)
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
                    System.Diagnostics.Debug.WriteLine($"Error fitting GlobalLogDirectoryWindow on screen: {ex.Message}");
                }
            }));
        }

        private const int WM_NCHITTEST = 0x0084;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_NCHITTEST || _isMinimized || !IsLoaded)
                return IntPtr.Zero;

            const int borderThickness = 8;
            var x = (int)(lParam.ToInt64() & 0xFFFF);
            var y = (int)((lParam.ToInt64() >> 16) & 0xFFFF);
            var point = PointFromScreen(new Point(x, y));
            var width = ActualWidth;
            var height = ActualHeight;

            if (width <= 0 || height <= 0 || double.IsNaN(width) || double.IsNaN(height))
                return IntPtr.Zero;

            if (point.X < borderThickness && point.Y < borderThickness)
            {
                handled = true;
                return new IntPtr(HTTOPLEFT);
            }

            if (point.X >= width - borderThickness && point.Y < borderThickness)
            {
                handled = true;
                return new IntPtr(HTTOPRIGHT);
            }

            if (point.X < borderThickness && point.Y >= height - borderThickness)
            {
                handled = true;
                return new IntPtr(HTBOTTOMLEFT);
            }

            if (point.X >= width - borderThickness && point.Y >= height - borderThickness)
            {
                handled = true;
                return new IntPtr(HTBOTTOMRIGHT);
            }

            if (point.Y < borderThickness && point.X >= borderThickness && point.X <= width - borderThickness)
            {
                handled = true;
                return new IntPtr(HTTOP);
            }

            if (point.Y >= height - borderThickness && point.X >= borderThickness && point.X <= width - borderThickness)
            {
                handled = true;
                return new IntPtr(HTBOTTOM);
            }

            if (point.X < borderThickness && point.Y >= borderThickness && point.Y <= height - borderThickness)
            {
                handled = true;
                return new IntPtr(HTLEFT);
            }

            if (point.X >= width - borderThickness && point.Y >= borderThickness && point.Y <= height - borderThickness)
            {
                handled = true;
                return new IntPtr(HTRIGHT);
            }

            return IntPtr.Zero;
        }

        private static string WrapLongLines(string text, int maxLineLength = 120)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var result = new StringBuilder(text.Length + text.Length / maxLineLength);
            foreach (var line in text.Split('\n'))
            {
                if (line.Length <= maxLineLength)
                {
                    result.AppendLine(line);
                    continue;
                }

                for (var i = 0; i < line.Length; i += maxLineLength)
                {
                    var length = Math.Min(maxLineLength, line.Length - i);
                    result.AppendLine(line.Substring(i, length));
                }
            }

            return result.ToString().TrimEnd('\r', '\n');
        }
    }
}
