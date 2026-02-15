using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using Microsoft.Win32;

namespace HouseVictoria.App.Screens.Windows
{
    public class DataBankManagementWindowViewModel : ObservableObject
    {
        private readonly IMemoryService _memoryService;

        // Collections
        private readonly ObservableCollection<DataBankViewModel> _allDataBanks = new();
        private readonly ObservableCollection<DataBankEntryViewModel> _allEntries = new();

        // Selected bank
        private DataBankViewModel? _selectedDataBank;
        private ObservableCollection<DataBankEntryViewModel> _selectedDataBankEntries = new();

        // Search / filter
        private string _searchQuery = string.Empty;
        private string _entrySearchQuery = string.Empty;
        private string _selectedCategory = "All Categories";
        private double _minimumImportance = 0;
        private readonly ObservableCollection<string> _availableCategories = new();

        public ObservableCollection<DataBankViewModel> FilteredDataBanks { get; }
        public ObservableCollection<DataBankEntryViewModel> SelectedDataBankEntries 
        { 
            get => _selectedDataBankEntries;
            private set => SetProperty(ref _selectedDataBankEntries, value);
        }

        public ObservableCollection<string> AvailableCategories => _availableCategories;

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    ApplyFilter();
                }
            }
        }

        public string EntrySearchQuery
        {
            get => _entrySearchQuery;
            set
            {
                if (SetProperty(ref _entrySearchQuery, value))
                {
                    ApplyEntryFilters();
                }
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    ApplyEntryFilters();
                }
            }
        }

        public double MinimumImportance
        {
            get => _minimumImportance;
            set
            {
                if (SetProperty(ref _minimumImportance, value))
                {
                    ApplyEntryFilters();
                }
            }
        }

        public string SelectedDataBankName => _selectedDataBank?.Name ?? "No data bank selected";
        public bool HasSelectedDataBank => _selectedDataBank != null;

        // Commands
        public ICommand CreateDataBankCommand { get; }
        public ICommand EditDataBankCommand { get; }
        public ICommand DeleteDataBankCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddEntryCommand { get; }
        public ICommand UploadFilesCommand { get; }
        public ICommand EditEntryCommand { get; }
        public ICommand RemoveEntryCommand { get; }
        public ICommand ClearEntriesCommand { get; }

        public DataBankManagementWindowViewModel(IMemoryService memoryService)
        {
            _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));

            FilteredDataBanks = new ObservableCollection<DataBankViewModel>();
            _availableCategories.Add("All Categories");
            SelectedCategory = _availableCategories.First();
            MinimumImportance = 0;

            CreateDataBankCommand = new RelayCommand(async () => await CreateDataBankAsync());
            EditDataBankCommand = new RelayCommand<DataBankViewModel>(async (bank) => await EditDataBankAsync(bank));
            DeleteDataBankCommand = new RelayCommand<DataBankViewModel>(async (bank) => await DeleteDataBankAsync(bank));
            RefreshCommand = new RelayCommand(async () => await LoadDataBanksAsync());
            AddEntryCommand = new RelayCommand(async () => await AddEntryAsync());
            UploadFilesCommand = new RelayCommand(async () => await UploadFilesAsync());
            EditEntryCommand = new RelayCommand<DataBankEntryViewModel>(async entry => await EditEntryAsync(entry));
            RemoveEntryCommand = new RelayCommand<DataBankEntryViewModel>(async (entry) => await RemoveEntryAsync(entry));
            ClearEntriesCommand = new RelayCommand(async () => await ClearEntriesAsync());

            // Load existing data banks asynchronously
            _ = LoadDataBanksAsync().ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadDataBanksAsync failed: {task.Exception.GetBaseException().Message}");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void ApplyFilter()
        {
            var query = _allDataBanks.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                var searchLower = _searchQuery.ToLowerInvariant();
                query = query.Where(b => 
                    b.Name.ToLowerInvariant().Contains(searchLower) ||
                    (b.Description != null && b.Description.ToLowerInvariant().Contains(searchLower)));
            }

            var app = Application.Current;
            if (app?.Dispatcher != null)
            {
                app.Dispatcher.Invoke(() =>
                {
                    FilteredDataBanks.Clear();
                    foreach (var bank in query.OrderBy(b => b.Name))
                    {
                        FilteredDataBanks.Add(bank);
                    }
                });
            }
        }

        private void ApplyEntryFilters()
        {
            var query = _allEntries.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_entrySearchQuery))
            {
                var searchLower = _entrySearchQuery.ToLowerInvariant();
                query = query.Where(e =>
                    e.Title.ToLowerInvariant().Contains(searchLower) ||
                    (!string.IsNullOrWhiteSpace(e.Content) && e.Content.ToLowerInvariant().Contains(searchLower)));
            }

            if (!string.IsNullOrWhiteSpace(_selectedCategory) && _selectedCategory != "All Categories")
            {
                query = query.Where(e => string.Equals(e.Category ?? string.Empty, _selectedCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (_minimumImportance > 0)
            {
                query = query.Where(e => e.Importance >= _minimumImportance);
            }

            var app = Application.Current;
            if (app?.Dispatcher != null)
            {
                app.Dispatcher.Invoke(() =>
                {
                    SelectedDataBankEntries.Clear();
                    foreach (var entry in query.OrderByDescending(e => e.LastModified))
                    {
                        SelectedDataBankEntries.Add(entry);
                    }
                });
            }
        }

        private void UpdateCategoriesFromEntries()
        {
            _availableCategories.Clear();
            _availableCategories.Add("All Categories");

            foreach (var cat in _allEntries.Select(e => e.Category)
                                           .Where(c => !string.IsNullOrWhiteSpace(c))
                                           .Distinct(StringComparer.OrdinalIgnoreCase)
                                           .OrderBy(c => c))
            {
                _availableCategories.Add(cat!);
            }

            if (!_availableCategories.Contains(_selectedCategory))
            {
                SelectedCategory = _availableCategories.First();
            }
        }

        public async Task LoadDataBanksAsync()
        {
            try
            {
                var banks = await _memoryService.GetAllDataBanksAsync();
                
                if (banks == null)
                {
                    System.Diagnostics.Debug.WriteLine("GetAllDataBanksAsync returned null");
                    return;
                }
                
                var app = Application.Current;
                if (app == null || app.Dispatcher == null)
                {
                    System.Diagnostics.Debug.WriteLine("Application or Dispatcher is null, cannot update UI");
                    return;
                }
                
                DataBankViewModel? reselectedBank = null;

                await app.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        _allDataBanks.Clear();
                        foreach (var bank in banks)
                        {
                            if (bank != null)
                            {
                                _allDataBanks.Add(new DataBankViewModel(bank));
                            }
                        }
                        
                        ApplyFilter();
                        
                        // If selected bank still exists, reselect it
                        if (_selectedDataBank != null)
                        {
                            reselectedBank = _allDataBanks.FirstOrDefault(b => b.Id == _selectedDataBank.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error adding data banks to collection: {ex.Message}\n{ex.StackTrace}");
                    }
                }, System.Windows.Threading.DispatcherPriority.Normal);

                if (reselectedBank != null)
                {
                    await SelectDataBankAsync(reselectedBank.Id);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data banks: {ex.Message}\n{ex.StackTrace}");
                try
                {
                    var app = Application.Current;
                    if (app != null && app.Dispatcher != null)
                    {
                        await app.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show($"Error loading data banks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                }
                catch
                {
                    // If we can't show message box, at least log it
                    System.Diagnostics.Debug.WriteLine($"Could not show error message box: {ex.Message}");
                }
            }
        }

        public async Task SelectDataBankAsync(string bankId)
        {
            try
            {
                var bank = await _memoryService.GetDataBankAsync(bankId);
                if (bank == null)
                {
                    MessageBox.Show("Data bank not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var app = Application.Current;
                if (app?.Dispatcher != null)
                {
                    await app.Dispatcher.InvokeAsync(() =>
                    {
                        _selectedDataBank = _allDataBanks.FirstOrDefault(b => b.Id == bankId) ?? new DataBankViewModel(bank);
                        _allEntries.Clear();
                        if (bank.DataEntries != null)
                        {
                            foreach (var entry in bank.DataEntries)
                            {
                                _allEntries.Add(new DataBankEntryViewModel(entry, bank.Id));
                            }
                        }
                        SelectedDataBankEntries = new ObservableCollection<DataBankEntryViewModel>(_allEntries);
                        UpdateCategoriesFromEntries();
                        ApplyEntryFilters();
                        OnPropertyChanged(nameof(SelectedDataBankName));
                        OnPropertyChanged(nameof(HasSelectedDataBank));
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting data bank: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in SelectDataBankAsync: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task CreateDataBankAsync()
        {
            try
            {
                var dialog = new CreateDataBankDialog();
                dialog.Owner = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                
                var result = dialog.ShowDialog();
                
                if (result == true && dialog.CreatedDataBank != null)
                {
                    await _memoryService.AddDataBankAsync(dialog.CreatedDataBank);
                    await LoadDataBanksAsync();
                    
                    // Select the newly created bank
                    if (dialog.CreatedDataBank.Id != null)
                    {
                        await SelectDataBankAsync(dialog.CreatedDataBank.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating data bank: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in CreateDataBankAsync: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task EditDataBankAsync(DataBankViewModel? bankViewModel)
        {
            if (bankViewModel == null) return;

            try
            {
                var bank = await _memoryService.GetDataBankAsync(bankViewModel.Id);
                if (bank == null)
                {
                    MessageBox.Show("Data bank not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dialog = new CreateDataBankDialog(bank);
                dialog.Owner = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                
                var result = dialog.ShowDialog();
                
                if (result == true && dialog.CreatedDataBank != null)
                {
                    await _memoryService.AddDataBankAsync(dialog.CreatedDataBank);
                    await LoadDataBanksAsync();
                    
                    // Reselect the edited bank
                    await SelectDataBankAsync(dialog.CreatedDataBank.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error editing data bank: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in EditDataBankAsync: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task DeleteDataBankAsync(DataBankViewModel? bankViewModel)
        {
            if (bankViewModel == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete the data bank '{bankViewModel.Name}'?\n\nThis action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _memoryService.DeleteDataBankAsync(bankViewModel.Id);
                await LoadDataBanksAsync();
                
                // Clear selection if the deleted bank was selected
                if (_selectedDataBank?.Id == bankViewModel.Id)
                {
                    _selectedDataBank = null;
                    SelectedDataBankEntries = new ObservableCollection<DataBankEntryViewModel>();
                    _availableCategories.Clear();
                    _availableCategories.Add("All Categories");
                    SelectedCategory = _availableCategories.First();
                    ApplyEntryFilters();
                    OnPropertyChanged(nameof(SelectedDataBankName));
                    OnPropertyChanged(nameof(HasSelectedDataBank));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting data bank: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in DeleteDataBankAsync: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task AddEntryAsync()
        {
            if (_selectedDataBank == null)
            {
                MessageBox.Show("Please select a data bank first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var dialog = new AddDataEntryDialog();
                dialog.Owner = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                
                var result = dialog.ShowDialog();
                if (result == true && dialog.ResultEntry != null)
                {
                    await _memoryService.AddDataToBankAsync(_selectedDataBank.Id, dialog.ResultEntry);
                    await LoadDataBanksAsync();
                    await SelectDataBankAsync(_selectedDataBank.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding entry: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in AddEntryAsync: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task UploadFilesAsync()
        {
            if (_selectedDataBank == null)
            {
                MessageBox.Show("Please select a data bank before uploading files.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Upload files to data bank",
                Filter = "Documents and media|*.pdf;*.txt;*.md;*.doc;*.docx;*.rtf;*.csv;*.json;*.xml;*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.zip;*.7z;*.ppt;*.pptx;*.xls;*.xlsx|All files|*.*"
            };

            var result = dialog.ShowDialog();
            if (result != true || dialog.FileNames.Length == 0)
            {
                return;
            }

            int successCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            foreach (var filePath in dialog.FileNames)
            {
                try
                {
                    var info = new FileInfo(filePath);
                    var extension = info.Extension?.Trim('.').ToLowerInvariant() ?? "file";

                    var entry = new DataBankEntry
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = Path.GetFileNameWithoutExtension(info.Name),
                        Content = $"Uploaded file: {info.Name}\nSize: {info.Length:N0} bytes\nOriginal Path: {info.FullName}",
                        Category = string.IsNullOrWhiteSpace(extension) ? "FILE" : extension.ToUpperInvariant(),
                        Tags = new List<string> { "uploaded-file", extension },
                        Importance = 0.5,
                        CreatedAt = DateTime.Now,
                        LastModified = DateTime.Now,
                        AttachmentTempPath = info.FullName,
                        AttachmentFileName = info.Name,
                        AttachmentContentType = extension,
                        AttachmentSizeBytes = info.Length
                    };

                    await _memoryService.AddDataToBankAsync(_selectedDataBank.Id, entry);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Error uploading {filePath}: {ex.Message}");
                }
            }

            await LoadDataBanksAsync();
            await SelectDataBankAsync(_selectedDataBank.Id);

            var message = $"Uploaded {successCount} file(s) to '{_selectedDataBank.Name}'.";
            if (errorCount > 0)
            {
                message += $"\n\n{errorCount} file(s) failed:\n" + string.Join("\n", errors.Take(5));
                if (errors.Count > 5)
                {
                    message += $"\n...and {errors.Count - 5} more.";
                }
            }

            MessageBox.Show(message, "Upload complete", MessageBoxButton.OK, errorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private async Task EditEntryAsync(DataBankEntryViewModel? entryViewModel)
        {
            if (_selectedDataBank == null || entryViewModel == null)
            {
                return;
            }

            try
            {
                var dialog = new AddDataEntryDialog(entryViewModel.ToEntry());
                dialog.Owner = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

                var result = dialog.ShowDialog();
                if (result == true && dialog.ResultEntry != null)
                {
                    // Preserve the entry identifier
                    dialog.ResultEntry.Id = entryViewModel.Id;
                    dialog.ResultEntry.CreatedAt = entryViewModel.CreatedAt;
                    await _memoryService.UpdateDataBankEntryAsync(_selectedDataBank.Id, dialog.ResultEntry);
                    await LoadDataBanksAsync();
                    await SelectDataBankAsync(_selectedDataBank.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error editing entry: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in EditEntryAsync: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task RemoveEntryAsync(DataBankEntryViewModel? entryViewModel)
        {
            if (_selectedDataBank == null || entryViewModel == null)
                return;

            try
            {
                await _memoryService.DeleteDataBankEntryAsync(_selectedDataBank.Id, entryViewModel.Id);
                await LoadDataBanksAsync();
                await SelectDataBankAsync(_selectedDataBank.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing entry: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in RemoveEntryAsync: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task ClearEntriesAsync()
        {
            if (_selectedDataBank == null)
                return;

            var result = MessageBox.Show(
                $"Are you sure you want to clear all entries from '{_selectedDataBank.Name}'?\n\nThis action cannot be undone.",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var bank = await _memoryService.GetDataBankAsync(_selectedDataBank.Id);
                if (bank == null) return;

                bank.DataEntries.Clear();
                bank.LastModified = DateTime.Now;
                await _memoryService.AddDataBankAsync(bank);
                await LoadDataBanksAsync();
                await SelectDataBankAsync(_selectedDataBank.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing entries: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in ClearEntriesAsync: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    public class DataBankViewModel : ObservableObject
    {
        private readonly DataBank _dataBank;

        public DataBankViewModel(DataBank dataBank)
        {
            _dataBank = dataBank ?? throw new ArgumentNullException(nameof(dataBank));
        }

        public string Id => _dataBank?.Id ?? string.Empty;
        public string Name => _dataBank?.Name ?? string.Empty;
        public string? Description => _dataBank?.Description;
        public int EntryCount => _dataBank?.DataEntries?.Count ?? 0;
        public DateTime LastModified => _dataBank?.LastModified ?? DateTime.Now;
        public DateTime CreatedAt => _dataBank?.CreatedAt ?? DateTime.Now;
    }

    public class DataBankEntryViewModel : ObservableObject
    {
        private readonly DataBankEntry _entry;
        private readonly string _bankId;

        public DataBankEntryViewModel(DataBankEntry entry, string bankId)
        {
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
            _bankId = bankId;
        }

        public string BankId => _bankId;
        public string Id => _entry.Id;
        public string Title => string.IsNullOrWhiteSpace(_entry.Title) ? "Untitled Entry" : _entry.Title;
        public string Content => _entry.Content ?? string.Empty;
        public string? Category => _entry.Category;
        public double Importance => _entry.Importance;
        public DateTime CreatedAt => _entry.CreatedAt;
        public DateTime LastModified => _entry.LastModified;
        public string TagsDisplay => _entry.Tags != null && _entry.Tags.Any()
            ? string.Join(", ", _entry.Tags)
            : "No tags";

        public DataBankEntry ToEntry()
        {
            return new DataBankEntry
            {
                Id = _entry.Id,
                Title = _entry.Title,
                Content = _entry.Content,
                Category = _entry.Category,
                Tags = _entry.Tags ?? new List<string>(),
                Importance = _entry.Importance,
                CreatedAt = _entry.CreatedAt,
                LastModified = _entry.LastModified,
                AttachmentPath = _entry.AttachmentPath,
                AttachmentFileName = _entry.AttachmentFileName,
                AttachmentContentType = _entry.AttachmentContentType,
                AttachmentSizeBytes = _entry.AttachmentSizeBytes
            };
        }

        public bool HasAttachment => !string.IsNullOrWhiteSpace(_entry.AttachmentPath) || !string.IsNullOrWhiteSpace(_entry.AttachmentFileName);

        public string AttachmentSummary
        {
            get
            {
                if (!HasAttachment)
                {
                    return "No attachment";
                }

                var name = _entry.AttachmentFileName ?? System.IO.Path.GetFileName(_entry.AttachmentPath);
                var size = _entry.AttachmentSizeBytes.HasValue ? $" ({FormatSize(_entry.AttachmentSizeBytes.Value)})" : string.Empty;
                return $"📎 {name}{size}";
            }
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }
    }
}
