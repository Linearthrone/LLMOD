using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using Microsoft.Win32;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public class ProjectDetailDialogViewModel : ObservableObject
    {
        private readonly IProjectManagementService _projectManagementService;
        private readonly IPersistenceService _persistenceService;
        private Project _originalProject;
        private Project _currentProject;

        // Edit mode
        private bool _isReadOnly = true;
        private bool _isEditable = false;

        // Form fields
        private string _projectName = string.Empty;
        private string _description = string.Empty;
        private ProjectType _selectedType = ProjectType.Other;
        private int _priority = 5;
        private DateTime _startDate = DateTime.Now;
        private DateTime _deadline = DateTime.Now.AddDays(30);
        private DateTime _createdAt = DateTime.Now;
        private ProjectPhase _selectedPhase = ProjectPhase.Planning;
        private AIContactOption? _selectedAIContact;
        private ObservableCollection<string> _roadblocks = new();
        private string _newRoadblockText = string.Empty;
        private double _completionPercentage = 0;

        // Collections
        private readonly ObservableCollection<ProjectType> _projectTypes = new();
        private readonly ObservableCollection<ProjectPhase> _projectPhases = new();
        private readonly ObservableCollection<AIContact> _aiContacts = new();
        private readonly ObservableCollection<AIContactOption> _aiContactsWithNone = new();
        private readonly ObservableCollection<ProjectArtifactViewModel> _artifacts = new();
        private readonly ObservableCollection<ProjectLogViewModel> _logs = new();
        private readonly ObservableCollection<ProjectLogViewModel> _filteredLogs = new();

        // Filtering
        private AIContactOption? _selectedFilterAIContact;
        private string _logSearchText = string.Empty;

        public ObservableCollection<ProjectType> ProjectTypes => _projectTypes;
        public ObservableCollection<ProjectPhase> ProjectPhases => _projectPhases;
        public ObservableCollection<AIContact> AIContacts => _aiContacts;
        public ObservableCollection<AIContactOption> AIContactsWithNone => _aiContactsWithNone;
        public ObservableCollection<string> Roadblocks => _roadblocks;
        public ObservableCollection<ProjectArtifactViewModel> Artifacts => _artifacts;
        public ObservableCollection<ProjectLogViewModel> FilteredLogs => _filteredLogs;

        // Commands
        public ICommand ToggleEditCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand DeleteProjectCommand { get; }
        public ICommand AddRoadblockCommand { get; }
        public ICommand RemoveRoadblockCommand { get; }
        public ICommand UploadArtifactCommand { get; }
        public ICommand PreviewArtifactCommand { get; }
        public ICommand DownloadArtifactCommand { get; }
        public ICommand DeleteArtifactCommand { get; }
        public ICommand ClearLogSearchCommand { get; }

        // Events
        public event EventHandler? ProjectDeleted;
        public event EventHandler? ProjectSaved;

        // Properties
        public string ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public ProjectType SelectedType
        {
            get => _selectedType;
            set => SetProperty(ref _selectedType, value);
        }

        public int Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        public DateTime Deadline
        {
            get => _deadline;
            set
            {
                if (SetProperty(ref _deadline, value))
                {
                    OnPropertyChanged(nameof(DaysRemaining));
                    OnPropertyChanged(nameof(DaysRemainingColor));
                }
            }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public ProjectPhase SelectedPhase
        {
            get => _selectedPhase;
            set
            {
                if (SetProperty(ref _selectedPhase, value))
                {
                    UpdateCompletionPercentage();
                }
            }
        }

        public AIContactOption? SelectedAIContact
        {
            get => _selectedAIContact;
            set => SetProperty(ref _selectedAIContact, value);
        }

        public string NewRoadblockText
        {
            get => _newRoadblockText;
            set
            {
                if (SetProperty(ref _newRoadblockText, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public double CompletionPercentage
        {
            get => _completionPercentage;
            set => SetProperty(ref _completionPercentage, value);
        }

        public bool IsReadOnly
        {
            get => _isReadOnly;
            set => SetProperty(ref _isReadOnly, value);
        }

        public bool IsEditable
        {
            get => _isEditable;
            set => SetProperty(ref _isEditable, value);
        }

        public AIContactOption? SelectedFilterAIContact
        {
            get => _selectedFilterAIContact;
            set
            {
                if (SetProperty(ref _selectedFilterAIContact, value))
                {
                    FilterLogs();
                }
            }
        }

        public string LogSearchText
        {
            get => _logSearchText;
            set
            {
                if (SetProperty(ref _logSearchText, value))
                {
                    FilterLogs();
                }
            }
        }

        // Computed properties
        public string ProjectTypePhase => $"{_selectedType} • {_selectedPhase}";
        public int RoadblocksCount => _roadblocks?.Count ?? 0;
        public bool HasRoadblocks => RoadblocksCount > 0;
        public int ArtifactsCount => _artifacts?.Count ?? 0;
        public bool HasArtifacts => ArtifactsCount > 0;
        public int LogsCount => _logs?.Count ?? 0;
        public bool HasLogs => LogsCount > 0;
        public int DaysRemaining => (Deadline - DateTime.Now).Days;
        public Brush DaysRemainingColor
        {
            get
            {
                var days = DaysRemaining;
                if (days < 0) return new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)); // Red (overdue)
                if (days <= 7) return new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)); // Orange (urgent)
                return new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Green (good)
            }
        }

        public ProjectDetailDialogViewModel(Project project, IProjectManagementService projectManagementService, IPersistenceService persistenceService)
        {
            _projectManagementService = projectManagementService ?? throw new ArgumentNullException(nameof(projectManagementService));
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            _originalProject = project;
            _currentProject = new Project
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                Type = project.Type,
                Priority = project.Priority,
                StartDate = project.StartDate,
                Deadline = project.Deadline,
                Phase = project.Phase,
                CompletionPercentage = project.CompletionPercentage,
                Roadblocks = project.Roadblocks?.ToList() ?? new List<string>(),
                AssignedAIId = project.AssignedAIId,
                CreatedAt = project.CreatedAt,
                LastModifiedAt = project.LastModifiedAt
            };

            LoadProjectData();

            // Initialize enum collections
            foreach (ProjectType type in Enum.GetValues(typeof(ProjectType)))
            {
                _projectTypes.Add(type);
            }

            foreach (ProjectPhase phase in Enum.GetValues(typeof(ProjectPhase)))
            {
                _projectPhases.Add(phase);
            }

            // Initialize commands
            ToggleEditCommand = new RelayCommand(() => { IsReadOnly = false; IsEditable = true; });
            SaveProjectCommand = new RelayCommand(async () => await SaveProjectAsync(), () => IsEditable);
            CancelEditCommand = new RelayCommand(() => 
            {
                LoadProjectData();
                IsReadOnly = true;
                IsEditable = false;
            }, () => IsEditable);
            DeleteProjectCommand = new RelayCommand(async () => await DeleteProjectAsync());
            AddRoadblockCommand = new RelayCommand(() => AddRoadblock(), () => IsEditable && !string.IsNullOrWhiteSpace(NewRoadblockText?.Trim()));
            RemoveRoadblockCommand = new RelayCommand((param) => RemoveRoadblock(param as string), (param) => IsEditable);
            UploadArtifactCommand = new RelayCommand(async () => await UploadArtifactAsync(), () => IsEditable);
            PreviewArtifactCommand = new RelayCommand((param) => PreviewArtifact(param as ProjectArtifactViewModel));
            DownloadArtifactCommand = new RelayCommand((param) => DownloadArtifact(param as ProjectArtifactViewModel));
            DeleteArtifactCommand = new RelayCommand(async (param) => await DeleteArtifactAsync(param as ProjectArtifactViewModel), (param) => IsEditable);
            ClearLogSearchCommand = new RelayCommand(() => { LogSearchText = string.Empty; SelectedFilterAIContact = _aiContactsWithNone.FirstOrDefault(); });

            // Load AI contacts and data
            _ = LoadAIContactsAsync();
            _ = LoadArtifactsAsync();
            _ = LoadLogsAsync();
        }

        private void LoadProjectData()
        {
            ProjectName = _currentProject.Name;
            Description = _currentProject.Description ?? string.Empty;
            SelectedType = _currentProject.Type;
            Priority = _currentProject.Priority;
            StartDate = _currentProject.StartDate;
            Deadline = _currentProject.Deadline;
            CreatedAt = _currentProject.CreatedAt;
            SelectedPhase = _currentProject.Phase;
            CompletionPercentage = _currentProject.CompletionPercentage;

            _roadblocks.Clear();
            if (_currentProject.Roadblocks != null)
            {
                foreach (var roadblock in _currentProject.Roadblocks)
                {
                    _roadblocks.Add(roadblock);
                }
            }

            OnPropertyChanged(nameof(ProjectTypePhase));
            OnPropertyChanged(nameof(RoadblocksCount));
            OnPropertyChanged(nameof(HasRoadblocks));
            OnPropertyChanged(nameof(DaysRemaining));
            OnPropertyChanged(nameof(DaysRemainingColor));
        }

        private async Task LoadAIContactsAsync()
        {
            try
            {
                var contacts = await _persistenceService.GetAllAsync<AIContact>();
                _aiContacts.Clear();
                _aiContactsWithNone.Clear();

                var noneOption = new AIContactOption { Id = null, Name = "None" };
                _aiContactsWithNone.Add(noneOption);

                foreach (var contact in contacts.Values)
                {
                    _aiContacts.Add(contact);
                    _aiContactsWithNone.Add(new AIContactOption { Id = contact.Id, Name = contact.Name });
                }

                // Set selected AI contact
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (string.IsNullOrWhiteSpace(_currentProject.AssignedAIId))
                    {
                        SelectedAIContact = noneOption;
                    }
                    else
                    {
                        SelectedAIContact = _aiContactsWithNone.FirstOrDefault(c => c.Id == _currentProject.AssignedAIId) ?? noneOption;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading AI contacts: {ex.Message}");
            }
        }

        private async Task LoadArtifactsAsync()
        {
            try
            {
                var artifacts = await _projectManagementService.GetArtifactsAsync(_currentProject.Id);
                
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _artifacts.Clear();
                    foreach (var artifact in artifacts)
                    {
                        _artifacts.Add(new ProjectArtifactViewModel(artifact));
                    }
                    OnPropertyChanged(nameof(ArtifactsCount));
                    OnPropertyChanged(nameof(HasArtifacts));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading artifacts: {ex.Message}");
            }
        }

        private async Task LoadLogsAsync()
        {
            try
            {
                var logs = await _projectManagementService.GetProjectLogsAsync(_currentProject.Id);
                
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _logs.Clear();
                    _filteredLogs.Clear();
                    foreach (var log in logs.OrderByDescending(l => l.Timestamp))
                    {
                        var logViewModel = new ProjectLogViewModel(log);
                        _logs.Add(logViewModel);
                        _filteredLogs.Add(logViewModel);
                    }
                    OnPropertyChanged(nameof(LogsCount));
                    OnPropertyChanged(nameof(HasLogs));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading logs: {ex.Message}");
            }
        }

        private void UpdateCompletionPercentage()
        {
            CompletionPercentage = SelectedPhase switch
            {
                ProjectPhase.Planning => 10,
                ProjectPhase.Research => 25,
                ProjectPhase.Development => 50,
                ProjectPhase.Testing => 75,
                ProjectPhase.Review => 85,
                ProjectPhase.Deployment => 95,
                ProjectPhase.Completed => 100,
                _ => CompletionPercentage
            };
        }

        private void FilterLogs()
        {
            _filteredLogs.Clear();
            var query = _logs.AsEnumerable();

            // Filter by AI contact
            if (SelectedFilterAIContact != null && !string.IsNullOrWhiteSpace(SelectedFilterAIContact.Id))
            {
                query = query.Where(l => l.PerformedBy == SelectedFilterAIContact.Id);
            }

            // Filter by search text
            if (!string.IsNullOrWhiteSpace(LogSearchText))
            {
                var searchLower = LogSearchText.ToLower();
                query = query.Where(l => 
                    (l.Action?.ToLower().Contains(searchLower) ?? false) ||
                    (l.Details?.ToLower().Contains(searchLower) ?? false) ||
                    (l.PerformedBy?.ToLower().Contains(searchLower) ?? false));
            }

            foreach (var log in query)
            {
                _filteredLogs.Add(log);
            }
        }

        private void AddRoadblock()
        {
            var trimmed = NewRoadblockText?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || !IsEditable)
                return;

            if (_roadblocks.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show("This roadblock already exists.", "Duplicate Roadblock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _roadblocks.Add(trimmed);
            NewRoadblockText = string.Empty;
            OnPropertyChanged(nameof(RoadblocksCount));
            OnPropertyChanged(nameof(HasRoadblocks));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void RemoveRoadblock(string? roadblock)
        {
            if (string.IsNullOrWhiteSpace(roadblock) || !IsEditable)
                return;

            _roadblocks.Remove(roadblock);
            OnPropertyChanged(nameof(RoadblocksCount));
            OnPropertyChanged(nameof(HasRoadblocks));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private async Task SaveProjectAsync()
        {
            try
            {
                _currentProject.Name = ProjectName.Trim();
                _currentProject.Description = Description?.Trim() ?? string.Empty;
                _currentProject.Type = SelectedType;
                _currentProject.Priority = Priority;
                _currentProject.StartDate = StartDate;
                _currentProject.Deadline = Deadline;
                _currentProject.Phase = SelectedPhase;
                _currentProject.CompletionPercentage = CompletionPercentage;
                _currentProject.Roadblocks = _roadblocks.ToList();
                _currentProject.AssignedAIId = SelectedAIContact?.Id;

                // Update phase if it changed
                if (_currentProject.Phase != _originalProject.Phase)
                {
                    await _projectManagementService.UpdateProjectPhaseAsync(_currentProject.Id, SelectedPhase);
                }

                var updatedProject = await _projectManagementService.UpdateProjectAsync(_currentProject);
                
                // Update original project reference
                _originalProject = updatedProject;
                _currentProject = new Project
                {
                    Id = updatedProject.Id,
                    Name = updatedProject.Name,
                    Description = updatedProject.Description,
                    Type = updatedProject.Type,
                    Priority = updatedProject.Priority,
                    StartDate = updatedProject.StartDate,
                    Deadline = updatedProject.Deadline,
                    Phase = updatedProject.Phase,
                    CompletionPercentage = updatedProject.CompletionPercentage,
                    Roadblocks = updatedProject.Roadblocks?.ToList() ?? new List<string>(),
                    AssignedAIId = updatedProject.AssignedAIId,
                    CreatedAt = updatedProject.CreatedAt,
                    LastModifiedAt = updatedProject.LastModifiedAt
                };

                IsReadOnly = true;
                IsEditable = false;
                OnPropertyChanged(nameof(ProjectTypePhase));
                OnPropertyChanged(nameof(DaysRemaining));
                OnPropertyChanged(nameof(DaysRemainingColor));

                ProjectSaved?.Invoke(this, EventArgs.Empty);
                MessageBox.Show("Project saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error saving project: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task DeleteProjectAsync()
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete project '{ProjectName}'?\n\nThis action cannot be undone.",
                "Delete Project",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _projectManagementService.DeleteProjectAsync(_currentProject.Id);
                    ProjectDeleted?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Diagnostics.Debug.WriteLine($"Error deleting project: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        private async Task UploadArtifactAsync()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select File to Upload as Artifact",
                    Filter = "All Files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    var fileInfo = new FileInfo(dialog.FileName);
                    var fileName = fileInfo.Name;
                    var filePath = dialog.FileName;

                    // Determine artifact type from extension
                    var extension = fileInfo.Extension.ToLower();
                    var artifactType = extension switch
                    {
                        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => ArtifactType.Image,
                        ".mp4" or ".avi" or ".mov" or ".wmv" => ArtifactType.Video,
                        ".mp3" or ".wav" or ".ogg" => ArtifactType.Audio,
                        ".pdf" or ".doc" or ".docx" or ".txt" => ArtifactType.Document,
                        ".cs" or ".js" or ".py" or ".cpp" or ".h" => ArtifactType.Code,
                        ".xaml" or ".xml" or ".json" => ArtifactType.Diagram,
                        _ => ArtifactType.File
                    };

                    var artifact = new ProjectArtifact
                    {
                        ProjectId = _currentProject.Id,
                        Name = fileName,
                        FilePath = filePath,
                        Type = artifactType,
                        FileSize = fileInfo.Length,
                        CreatedBy = "User" // Could be enhanced to track actual user or AI
                    };

                    await _projectManagementService.AddArtifactAsync(_currentProject.Id, artifact);
                    
                    // Reload artifacts
                    await LoadArtifactsAsync();

                    MessageBox.Show($"Artifact '{fileName}' uploaded successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error uploading artifact: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error uploading artifact: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void PreviewArtifact(ProjectArtifactViewModel? artifact)
        {
            if (artifact == null || string.IsNullOrWhiteSpace(artifact.FilePath))
                return;

            try
            {
                if (File.Exists(artifact.FilePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = artifact.FilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show($"File not found: {artifact.FilePath}", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error previewing artifact: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void DownloadArtifact(ProjectArtifactViewModel? artifact)
        {
            if (artifact == null || string.IsNullOrWhiteSpace(artifact.FilePath))
                return;

            try
            {
                if (!File.Exists(artifact.FilePath))
                {
                    MessageBox.Show($"File not found: {artifact.FilePath}", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Title = "Save Artifact As",
                    FileName = artifact.Name,
                    Filter = "All Files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    File.Copy(artifact.FilePath, dialog.FileName, overwrite: true);
                    MessageBox.Show($"Artifact saved to:\n{dialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading artifact: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error downloading artifact: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task DeleteArtifactAsync(ProjectArtifactViewModel? artifact)
        {
            if (artifact == null || !IsEditable)
                return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete artifact '{artifact.Name}'?",
                "Delete Artifact",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _projectManagementService.DeleteArtifactAsync(_currentProject.Id, artifact.Id);
                    
                    // Reload artifacts to reflect the deletion
                    await LoadArtifactsAsync();
                    
                    MessageBox.Show("Artifact deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting artifact: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Diagnostics.Debug.WriteLine($"Error deleting artifact: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    // ViewModels for artifact and log display
    public class ProjectArtifactViewModel : ObservableObject
    {
        private readonly ProjectArtifact _artifact;

        public ProjectArtifactViewModel(ProjectArtifact artifact)
        {
            _artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
        }

        public string Id => _artifact?.Id ?? string.Empty;
        public string Name => _artifact?.Name ?? "Unnamed";
        public string FilePath => _artifact?.FilePath ?? string.Empty;
        public ArtifactType Type => _artifact?.Type ?? ArtifactType.File;
        public string Description => _artifact?.Description ?? string.Empty;
        public long FileSize => _artifact?.FileSize ?? 0;

        public string TypeIcon
        {
            get
            {
                return Type switch
                {
                    ArtifactType.Image => "🖼️",
                    ArtifactType.Video => "🎥",
                    ArtifactType.Audio => "🎵",
                    ArtifactType.Document => "📄",
                    ArtifactType.Code => "💻",
                    ArtifactType.Diagram => "📊",
                    ArtifactType.Transcript => "📝",
                    _ => "📁"
                };
            }
        }

        public string FileSizeFormatted
        {
            get
            {
                var size = FileSize;
                if (size < 1024) return $"{size} B";
                if (size < 1024 * 1024) return $"{size / 1024.0:F1} KB";
                return $"{size / (1024.0 * 1024.0):F1} MB";
            }
        }

        public string CreatedAtFormatted => _artifact?.CreatedAt.ToString("MMM dd, yyyy HH:mm") ?? string.Empty;
    }

    public class ProjectLogViewModel : ObservableObject
    {
        private readonly ProjectLog _log;

        public ProjectLogViewModel(ProjectLog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public string Id => _log?.Id ?? string.Empty;
        public string PerformedBy => _log?.PerformedBy ?? "System";
        public string Action => _log?.Action ?? string.Empty;
        public string? Details => _log?.Details;
        public DateTime Timestamp => _log?.Timestamp ?? DateTime.Now;
    }
}
