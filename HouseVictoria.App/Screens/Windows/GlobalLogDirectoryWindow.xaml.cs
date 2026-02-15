using System;
using System.Windows;
using System.Windows.Controls;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class GlobalLogDirectoryWindow : Window
    {
        public GlobalLogDirectoryWindowViewModel ViewModel { get; }
        
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
            var logContent = FindName("LogContent") as System.Windows.Controls.TextBlock;
            var logTags = FindName("LogTags") as System.Windows.Controls.ItemsControl;
            
            if (e.NewValue is LogCategoryViewModel selectedItem)
            {
                if (selectedItem.LogEntry != null)
                {
                    // This is a leaf node (actual log), show details
                    if (noSelectionHint != null) noSelectionHint.Visibility = Visibility.Collapsed;
                    if (logDetailsPanel != null) logDetailsPanel.Visibility = Visibility.Visible;

                    var entry = selectedItem.LogEntry;
                    ViewModel.SelectLogEntryAsync(entry.Id).ConfigureAwait(false);

                    if (logTitle != null) logTitle.Text = entry.Title;
                    if (logDateTime != null) logDateTime.Text = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                    if (logSeverity != null) logSeverity.Text = entry.Severity.ToString();
                    if (logSource != null) logSource.Text = entry.Source;
                    if (logSummary != null) logSummary.Text = entry.Summary;
                    if (logContent != null) logContent.Text = entry.Content;

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
                }
            }
            else
            {
                // Nothing selected
                if (noSelectionHint != null) noSelectionHint.Visibility = Visibility.Visible;
                if (logDetailsPanel != null) logDetailsPanel.Visibility = Visibility.Collapsed;
            }
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
    }
}
