using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public class GlobalLogDirectoryWindowViewModel : ObservableObject
    {
        private readonly ILoggingService _loggingService;
        private readonly Dispatcher _ui;
        private LogEntry? _selectedLogEntry;
        private ObservableCollection<LogCategoryViewModel> _categories = new();
        private bool _isLoading;
        private string? _selectedLogId;
        private bool _selectedIsArchived;
        private bool _showArchived;
        private string? _loadError;

        public void NotifyLogEntrySelected(string logId)
        {
            RunOnUi(() =>
            {
                _selectedLogId = logId;
                _selectedIsArchived = _selectedLogEntry?.IsArchived ?? false;
                OnPropertyChanged(nameof(SelectedIsArchived));
                CommandManager.InvalidateRequerySuggested();
            });
        }

        public bool ShowArchived
        {
            get => _showArchived;
            set
            {
                RunOnUi(() =>
                {
                    if (!SetProperty(ref _showArchived, value))
                        return;

                    _loggingService.IncludeArchived = value;
                    _ = RefreshLogsAsync();
                });
            }
        }

        public bool SelectedIsArchived => _selectedIsArchived;

        public string? LoadError
        {
            get => _loadError;
            private set => SetProperty(ref _loadError, value);
        }

        public ObservableCollection<LogCategoryViewModel> Categories
        {
            get => _categories;
            set => RunOnUi(() => SetProperty(ref _categories, value));
        }

        public LogEntry? SelectedLogEntry
        {
            get => _selectedLogEntry;
            set => RunOnUi(() => SetProperty(ref _selectedLogEntry, value));
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => RunOnUi(() => SetProperty(ref _isLoading, value));
        }

        public ICommand RefreshCommand { get; }
        public ICommand MarkAllReadCommand { get; }
        public ICommand ArchiveSelectedCommand { get; }
        public ICommand UnarchiveSelectedCommand { get; }
        public ICommand ExportCommand { get; }

        public GlobalLogDirectoryWindowViewModel(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _ui = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _showArchived = _loggingService.IncludeArchived;

            RefreshCommand = new RelayCommand(async () => await RefreshLogsAsync().ConfigureAwait(true));
            MarkAllReadCommand = new RelayCommand(async () => await MarkAllReadAsync().ConfigureAwait(true));
            ArchiveSelectedCommand = new RelayCommand(
                async () => await ArchiveSelectedAsync().ConfigureAwait(true),
                () => !string.IsNullOrWhiteSpace(_selectedLogId) && !_selectedIsArchived);
            UnarchiveSelectedCommand = new RelayCommand(
                async () => await UnarchiveSelectedAsync().ConfigureAwait(true),
                () => !string.IsNullOrWhiteSpace(_selectedLogId) && _selectedIsArchived);
            ExportCommand = new RelayCommand(async () => await ExportLogsAsync().ConfigureAwait(true));
        }

        public Task InitializeAsync() => RefreshLogsAsync();

        private async Task RefreshLogsAsync()
        {
            await RunOnUiAsync(() =>
            {
                IsLoading = true;
                LoadError = null;
            }).ConfigureAwait(true);

            try
            {
                await _loggingService.RefreshLogsAsync().ConfigureAwait(false);
                var categories = await _loggingService.PeekLogCategoriesAsync().ConfigureAwait(false);
                var tree = BuildCategoryTree(categories);
                await RunOnUiAsync(() => Categories = tree).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                var message = ex.InnerException == null ? ex.Message : $"{ex.Message} ({ex.InnerException.Message})";
                System.Diagnostics.Debug.WriteLine($"GLD Error refreshing logs: {ex}");
                await RunOnUiAsync(() =>
                {
                    LoadError = message;
                    Categories = new ObservableCollection<LogCategoryViewModel>
                    {
                        new LogCategoryViewModel
                        {
                            Name = $"Error Loading Review Inbox: {message}",
                            Tag = "error"
                        }
                    };
                }).ConfigureAwait(true);
            }
            finally
            {
                await RunOnUiAsync(() => IsLoading = false).ConfigureAwait(true);
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
                    TotalCount = category.TotalCount,
                    IsExpanded = true
                };

                foreach (var subCategory in category.SubCategories.Values.OrderBy(sc => sc.Name))
                {
                    var subCategoryVm = new LogCategoryViewModel
                    {
                        Name = $"{subCategory.DisplayName} ({subCategory.TotalCount})",
                        Tag = $"{category.Name}_{subCategory.Name}",
                        UnreadCount = subCategory.UnreadCount,
                        TotalCount = subCategory.TotalCount,
                        IsExpanded = false
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
                await _loggingService.MarkAllAsReadAsync().ConfigureAwait(false);
                await RefreshLogsAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error marking all as read: {ex.Message}");
            }
        }

        public Task SelectLogEntryAsync(LogEntry entry) =>
            RunOnUiAsync(() =>
            {
                _selectedLogId = entry.Id;
                SelectedLogEntry = entry;
                _selectedIsArchived = entry.IsArchived;
                OnPropertyChanged(nameof(SelectedIsArchived));
                CommandManager.InvalidateRequerySuggested();
            });

        public async Task UnarchiveSelectedAsync()
        {
            if (string.IsNullOrWhiteSpace(_selectedLogId))
                return;

            try
            {
                var logId = _selectedLogId;
                await _loggingService.UnarchiveAsync(logId).ConfigureAwait(false);
                await RefreshLogsAsync().ConfigureAwait(true);
                await RunOnUiAsync(() =>
                {
                    SelectedLogEntry = null;
                    _selectedLogId = null;
                    _selectedIsArchived = false;
                    OnPropertyChanged(nameof(SelectedIsArchived));
                    CommandManager.InvalidateRequerySuggested();
                }).ConfigureAwait(true);
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
                var logId = _selectedLogId;
                await _loggingService.ArchiveAsync(logId).ConfigureAwait(false);
                await RunOnUiAsync(() =>
                {
                    RemoveEntryFromTree(Categories, logId);
                    UpdateCategoryCounts(Categories);
                    SelectedLogEntry = null;
                    _selectedLogId = null;
                    CommandManager.InvalidateRequerySuggested();
                }).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error archiving log entry: {ex.Message}");
            }
        }

        private void RunOnUi(Action action)
        {
            if (_ui.CheckAccess())
                action();
            else
                _ui.Invoke(action);
        }

        private Task RunOnUiAsync(Action action)
        {
            if (_ui.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return _ui.InvokeAsync(action).Task;
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

                var showDialog = false;
                string? selectedPath = null;
                LogExportFormat format = LogExportFormat.Text;

                await RunOnUiAsync(() =>
                {
                    if (dialog.ShowDialog() == true)
                    {
                        showDialog = true;
                        selectedPath = dialog.FileName;
                        format = dialog.FilterIndex switch
                        {
                            1 => LogExportFormat.Text,
                            2 => LogExportFormat.Json,
                            3 => LogExportFormat.Csv,
                            _ => LogExportFormat.Text
                        };
                    }
                }).ConfigureAwait(true);

                if (!showDialog || string.IsNullOrWhiteSpace(selectedPath))
                    return;

                var options = new LogExportOptions
                {
                    Format = format,
                    IncludeRead = true,
                    IncludeUnread = true
                };

                await _loggingService.ExportLogsAsync(selectedPath, options).ConfigureAwait(false);
                await RunOnUiAsync(() =>
                    MessageBox.Show($"Review inbox exported to:\n{selectedPath}",
                        "Export Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                await RunOnUiAsync(() =>
                    MessageBox.Show($"Error exporting: {ex.Message}",
                        "Export Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error)).ConfigureAwait(true);
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
        private bool _isExpanded;
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

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
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
