using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
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
        private string? _selectedLogId;
        private bool _selectedIsArchived;
        private bool _showArchived;

        public void NotifyLogEntrySelected(string logId)
        {
            _selectedLogId = logId;
            _selectedIsArchived = _selectedLogEntry?.IsArchived ?? false;
            OnPropertyChanged(nameof(SelectedIsArchived));
            CommandManager.InvalidateRequerySuggested();
        }

        public bool ShowArchived
        {
            get => _showArchived;
            set
            {
                if (SetProperty(ref _showArchived, value))
                {
                    _loggingService.IncludeArchived = value;
                    _ = RefreshLogsAsync();
                }
            }
        }

        /// <summary>True when the currently selected entry is archived (so it can be restored).</summary>
        public bool SelectedIsArchived => _selectedIsArchived;

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
        public ICommand ArchiveSelectedCommand { get; }
        public ICommand UnarchiveSelectedCommand { get; }
        public ICommand ExportCommand { get; }

        public GlobalLogDirectoryWindowViewModel(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _showArchived = _loggingService.IncludeArchived;

            RefreshCommand = new RelayCommand(async () => await RefreshLogsAsync());
            MarkAllReadCommand = new RelayCommand(async () => await MarkAllReadAsync());
            ArchiveSelectedCommand = new RelayCommand(async () => await ArchiveSelectedAsync(), () => !string.IsNullOrWhiteSpace(_selectedLogId) && !_selectedIsArchived);
            UnarchiveSelectedCommand = new RelayCommand(async () => await UnarchiveSelectedAsync(), () => !string.IsNullOrWhiteSpace(_selectedLogId) && _selectedIsArchived);
            ExportCommand = new RelayCommand(async () => await ExportLogsAsync());

            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(async () => await RefreshLogsAsync()));
        }

        private async Task RefreshLogsAsync()
        {
            try
            {
                IsLoading = true;
                await _loggingService.RefreshLogsAsync();
                var categories = await _loggingService.GetLogCategoriesAsync();
                Categories = BuildCategoryTree(categories);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GLD Error refreshing logs: {ex.Message}");
                Categories = new ObservableCollection<LogCategoryViewModel>
                {
                    new LogCategoryViewModel
                    {
                        Name = $"Error Loading Review Inbox: {ex.Message}",
                        Tag = "error"
                    }
                };
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static ObservableCollection<LogCategoryViewModel> BuildCategoryTree(
            Dictionary<string, LogCategory> categories)
        {
            var categoryViewModels = new ObservableCollection<LogCategoryViewModel>();

            if (categories.Count == 0)
            {
                categoryViewModels.Add(new LogCategoryViewModel
                {
                    Name = "Inbox empty — nothing awaiting review",
                    Tag = "placeholder"
                });
                return categoryViewModels;
            }

            foreach (var category in categories.Values.OrderBy(c => c.Name))
            {
                var categoryVm = new LogCategoryViewModel
                {
                    Name = category.DisplayName,
                    Tag = category.Name,
                    UnreadCount = category.UnreadCount,
                    TotalCount = category.TotalCount
                };

                foreach (var subCategory in category.SubCategories.Values.OrderBy(sc => sc.Name))
                {
                    var subCategoryVm = new LogCategoryViewModel
                    {
                        Name = $"{subCategory.DisplayName} ({subCategory.TotalCount})",
                        Tag = $"{category.Name}_{subCategory.Name}",
                        UnreadCount = subCategory.UnreadCount,
                        TotalCount = subCategory.TotalCount
                    };

                    foreach (var entry in subCategory.Entries.OrderByDescending(e => e.Timestamp))
                    {
                        subCategoryVm.Children.Add(CreateEntryNode(entry));
                    }

                    categoryVm.Children.Add(subCategoryVm);
                }

                foreach (var entry in category.Entries.OrderByDescending(e => e.Timestamp))
                {
                    categoryVm.Children.Add(CreateEntryNode(entry));
                }

                categoryViewModels.Add(categoryVm);
            }

            return categoryViewModels;
        }

        private static LogCategoryViewModel CreateEntryNode(LogEntry entry) =>
            new()
            {
                Name = entry.IsArchived
                    ? $"[archived] {entry.Title} - {entry.Timestamp:MM/dd HH:mm}"
                    : $"{entry.Title} - {entry.Timestamp:MM/dd HH:mm}",
                Tag = entry.Id,
                LogEntry = entry
            };

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

        public Task SelectLogEntryAsync(LogEntry entry)
        {
            _selectedLogId = entry.Id;
            SelectedLogEntry = entry;
            _selectedIsArchived = entry.IsArchived;
            OnPropertyChanged(nameof(SelectedIsArchived));
            CommandManager.InvalidateRequerySuggested();
            return Task.CompletedTask;
        }

        public async Task UnarchiveSelectedAsync()
        {
            if (string.IsNullOrWhiteSpace(_selectedLogId))
                return;

            try
            {
                await _loggingService.UnarchiveAsync(_selectedLogId);
                await RefreshLogsAsync();
                SelectedLogEntry = null;
                _selectedLogId = null;
                _selectedIsArchived = false;
                OnPropertyChanged(nameof(SelectedIsArchived));
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unarchiving log entry: {ex.Message}");
            }
        }

        public async Task ArchiveSelectedAsync()
        {
            if (string.IsNullOrWhiteSpace(_selectedLogId))
                return;

            try
            {
                await _loggingService.ArchiveAsync(_selectedLogId);
                RemoveEntryFromTree(Categories, _selectedLogId);
                UpdateCategoryCounts(Categories);
                SelectedLogEntry = null;
                _selectedLogId = null;
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error archiving log entry: {ex.Message}");
            }
        }

        private static bool RemoveEntryFromTree(ObservableCollection<LogCategoryViewModel> nodes, string logId)
        {
            foreach (var node in nodes.ToList())
            {
                if (node.LogEntry != null && string.Equals(node.Tag, logId, StringComparison.Ordinal))
                {
                    nodes.Remove(node);
                    return true;
                }

                if (RemoveEntryFromTree(node.Children, logId))
                {
                    if (node.Children.Count == 0 && node.LogEntry == null)
                        nodes.Remove(node);
                    return true;
                }
            }

            return false;
        }

        private static void UpdateCategoryCounts(ObservableCollection<LogCategoryViewModel> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.LogEntry != null)
                    continue;

                UpdateCategoryCounts(node.Children);
                node.UnreadCount = node.Children.Sum(c => c.LogEntry != null ? (c.LogEntry.IsRead ? 0 : 1) : c.UnreadCount);
                node.TotalCount = node.Children.Sum(c => c.LogEntry != null ? 1 : c.TotalCount);
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
                    FileName = $"HouseVictoria_ReviewInbox_{DateTime.Now:yyyyMMdd_HHmmss}"
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
                    System.Windows.MessageBox.Show($"Review inbox exported to:\n{dialog.FileName}",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error exporting: {ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
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
