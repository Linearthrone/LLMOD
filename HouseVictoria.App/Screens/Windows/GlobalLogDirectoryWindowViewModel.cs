using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public class GlobalLogDirectoryWindowViewModel : ObservableObject
    {
        private readonly ILoggingService _loggingService;
        private LogEntry? _selectedLogEntry;
        private ObservableCollection<LogCategoryViewModel> _categories = new();
        private bool _isLoading;

        public ObservableCollection<LogCategoryViewModel> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        public LogEntry? SelectedLogEntry
        {
            get => _selectedLogEntry;
            set => SetProperty(ref _selectedLogEntry, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand MarkAllReadCommand { get; }
        public ICommand ExportCommand { get; }

        public GlobalLogDirectoryWindowViewModel(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));

            RefreshCommand = new RelayCommand(async () => await RefreshLogsAsync());
            MarkAllReadCommand = new RelayCommand(async () => await MarkAllReadAsync());
            ExportCommand = new RelayCommand(async () => await ExportLogsAsync());

            // Initialize on UI thread after construction
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(async () => await RefreshLogsAsync()));
        }

        private async Task RefreshLogsAsync()
        {
            try
            {
                IsLoading = true;
                System.Diagnostics.Debug.WriteLine("GLD: Starting log refresh...");
                
                // Force refresh by calling RefreshLogsAsync directly
                await _loggingService.RefreshLogsAsync();
                var categories = await _loggingService.GetLogCategoriesAsync();
                
                System.Diagnostics.Debug.WriteLine($"GLD: Loaded {categories.Count} categories");
                
                var categoryViewModels = new ObservableCollection<LogCategoryViewModel>();
                
                if (categories.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("GLD: No categories found - adding placeholder");
                    // Add a placeholder category to show that the system is working but no logs exist
                    var placeholderVm = new LogCategoryViewModel
                    {
                        Name = "No Logs Available",
                        Tag = "placeholder",
                        UnreadCount = 0,
                        TotalCount = 0
                    };
                    categoryViewModels.Add(placeholderVm);
                }
                else
                {
                    foreach (var category in categories.Values.OrderBy(c => c.Name))
                    {
                        var categoryVm = new LogCategoryViewModel
                        {
                            Name = category.DisplayName,
                            Tag = category.Name,
                            UnreadCount = category.UnreadCount,
                            TotalCount = category.TotalCount
                        };

                        // Add subcategories
                        foreach (var subCategory in category.SubCategories.Values.OrderBy(sc => sc.Name))
                        {
                            var subCategoryVm = new LogCategoryViewModel
                            {
                                Name = $"{subCategory.DisplayName} ({subCategory.TotalCount})",
                                Tag = $"{category.Name}_{subCategory.Name}",
                                UnreadCount = subCategory.UnreadCount,
                                TotalCount = subCategory.TotalCount
                            };

                            // Add entries to subcategory
                            foreach (var entry in subCategory.Entries.OrderByDescending(e => e.Timestamp))
                            {
                                var entryVm = new LogCategoryViewModel
                                {
                                    Name = $"{entry.Title} - {entry.Timestamp:MM/dd HH:mm}",
                                    Tag = entry.Id,
                                    LogEntry = entry
                                };
                                subCategoryVm.Children.Add(entryVm);
                            }

                            categoryVm.Children.Add(subCategoryVm);
                        }

                        // Add direct entries (entries without subcategory)
                        foreach (var entry in category.Entries.OrderByDescending(e => e.Timestamp))
                        {
                            var entryVm = new LogCategoryViewModel
                            {
                                Name = $"{entry.Title} - {entry.Timestamp:MM/dd HH:mm}",
                                Tag = entry.Id,
                                LogEntry = entry
                            };
                            categoryVm.Children.Add(entryVm);
                        }

                        categoryViewModels.Add(categoryVm);
                    }
                }

                Categories = categoryViewModels;
                System.Diagnostics.Debug.WriteLine($"GLD: Refresh complete. Total categories in UI: {categoryViewModels.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GLD Error refreshing logs: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GLD Stack trace: {ex.StackTrace}");
                
                // Add error category to UI so user knows something went wrong
                var errorVm = new LogCategoryViewModel
                {
                    Name = $"Error Loading Logs: {ex.Message}",
                    Tag = "error",
                    UnreadCount = 0,
                    TotalCount = 0
                };
                Categories = new ObservableCollection<LogCategoryViewModel> { errorVm };
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task MarkAllReadAsync()
        {
            try
            {
                await _loggingService.MarkAllAsReadAsync();
                await RefreshLogsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error marking all as read: {ex.Message}");
            }
        }

        private async Task ExportLogsAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|JSON Files (*.json)|*.json|CSV Files (*.csv)|*.csv",
                    DefaultExt = "txt",
                    FileName = $"HouseVictoria_Logs_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (dialog.ShowDialog() == true)
                {
                    var format = dialog.FilterIndex switch
                    {
                        1 => LogExportFormat.Text,
                        2 => LogExportFormat.Json,
                        3 => LogExportFormat.Csv,
                        _ => LogExportFormat.Text
                    };

                    var options = new LogExportOptions
                    {
                        Format = format,
                        IncludeRead = true,
                        IncludeUnread = true
                    };

                    await _loggingService.ExportLogsAsync(dialog.FileName, options);
                    System.Windows.MessageBox.Show($"Logs exported successfully to:\n{dialog.FileName}", 
                        "Export Complete", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error exporting logs: {ex.Message}", 
                    "Export Error", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
            }
        }

        public async Task SelectLogEntryAsync(string logId)
        {
            try
            {
                var entry = await _loggingService.GetLogEntryAsync(logId);
                if (entry != null && !entry.IsRead)
                {
                    await _loggingService.MarkAsReadAsync(logId);
                    entry.IsRead = true;
                    await RefreshLogsAsync();
                }
                SelectedLogEntry = entry;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error selecting log entry: {ex.Message}");
            }
        }
    }

    public class LogCategoryViewModel : ObservableObject
    {
        private string _name = string.Empty;
        private string _tag = string.Empty;
        private int _unreadCount;
        private int _totalCount;
        private LogEntry? _logEntry;
        private ObservableCollection<LogCategoryViewModel> _children = new();

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Tag
        {
            get => _tag;
            set => SetProperty(ref _tag, value);
        }

        public int UnreadCount
        {
            get => _unreadCount;
            set => SetProperty(ref _unreadCount, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        public LogEntry? LogEntry
        {
            get => _logEntry;
            set => SetProperty(ref _logEntry, value);
        }

        public ObservableCollection<LogCategoryViewModel> Children
        {
            get => _children;
            set => SetProperty(ref _children, value);
        }
    }
}
