using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public class ProjectsWindowViewModel : ObservableObject
    {
        private readonly IProjectManagementService _projectManagementService;
        private readonly IPersistenceService? _persistenceService;

        // Collections
        private readonly ObservableCollection<ProjectViewModel> _allProjects = new();
        private readonly ObservableCollection<FilterOption<ProjectType?>> _filterTypes = new();
        private readonly ObservableCollection<FilterOption<ProjectPhase?>> _filterPhases = new();
        private readonly ObservableCollection<SortOption> _sortOptions = new();

        // Filtering and sorting
        private FilterOption<ProjectType?>? _selectedFilterType;
        private FilterOption<ProjectPhase?>? _selectedFilterPhase;
        private SortOption? _selectedSortOption;

        public ObservableCollection<ProjectViewModel> Projects { get; }
        public ObservableCollection<ProjectViewModel> FilteredProjects { get; }
        public ObservableCollection<FilterOption<ProjectType?>> FilterTypes => _filterTypes;
        public ObservableCollection<FilterOption<ProjectPhase?>> FilterPhases => _filterPhases;
        public ObservableCollection<SortOption> SortOptions => _sortOptions;

        // Commands
        public ICommand CreateProjectCommand { get; }
        public ICommand RefreshCommand { get; }

        public FilterOption<ProjectType?>? SelectedFilterType
        {
            get => _selectedFilterType;
            set
            {
                if (SetProperty(ref _selectedFilterType, value))
                {
                    ApplyFiltersAndSort();
                }
            }
        }

        public FilterOption<ProjectPhase?>? SelectedFilterPhase
        {
            get => _selectedFilterPhase;
            set
            {
                if (SetProperty(ref _selectedFilterPhase, value))
                {
                    ApplyFiltersAndSort();
                }
            }
        }

        public SortOption? SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (SetProperty(ref _selectedSortOption, value))
                {
                    ApplyFiltersAndSort();
                }
            }
        }

        public ProjectsWindowViewModel(IProjectManagementService projectManagementService)
        {
            _projectManagementService = projectManagementService ?? throw new ArgumentNullException(nameof(projectManagementService));
            
            // Try to get persistence service, but don't fail if not available
            try
            {
                _persistenceService = App.GetService<IPersistenceService>();
            }
            catch
            {
                _persistenceService = null!; // Explicitly set to null for nullable reference
            }

            Projects = new ObservableCollection<ProjectViewModel>();
            FilteredProjects = new ObservableCollection<ProjectViewModel>();

            // Initialize filter options
            InitializeFiltersAndSort();

            CreateProjectCommand = new RelayCommand(async () => await CreateProjectAsync());
            RefreshCommand = new RelayCommand(async () => await LoadProjectsAsync());
            
            // Load existing projects asynchronously (fire and forget, but with error handling)
            _ = LoadProjectsAsync().ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadProjectsAsync failed: {task.Exception.GetBaseException().Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack: {task.Exception.GetBaseException().StackTrace}");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void InitializeFiltersAndSort()
        {
            // Type filters
            _filterTypes.Add(new FilterOption<ProjectType?> { Name = "All Types", Value = null });
            foreach (ProjectType type in Enum.GetValues(typeof(ProjectType)))
            {
                _filterTypes.Add(new FilterOption<ProjectType?> { Name = type.ToString(), Value = type });
            }

            // Phase filters
            _filterPhases.Add(new FilterOption<ProjectPhase?> { Name = "All Phases", Value = null });
            foreach (ProjectPhase phase in Enum.GetValues(typeof(ProjectPhase)))
            {
                _filterPhases.Add(new FilterOption<ProjectPhase?> { Name = phase.ToString(), Value = phase });
            }

            // Sort options
            _sortOptions.Add(new SortOption { Name = "Name (A-Z)", SortKey = "Name", Ascending = true });
            _sortOptions.Add(new SortOption { Name = "Name (Z-A)", SortKey = "Name", Ascending = false });
            _sortOptions.Add(new SortOption { Name = "Priority (High-Low)", SortKey = "Priority", Ascending = false });
            _sortOptions.Add(new SortOption { Name = "Priority (Low-High)", SortKey = "Priority", Ascending = true });
            _sortOptions.Add(new SortOption { Name = "Deadline (Soonest)", SortKey = "Deadline", Ascending = true });
            _sortOptions.Add(new SortOption { Name = "Deadline (Latest)", SortKey = "Deadline", Ascending = false });
            _sortOptions.Add(new SortOption { Name = "Progress (High-Low)", SortKey = "Completion", Ascending = false });
            _sortOptions.Add(new SortOption { Name = "Progress (Low-High)", SortKey = "Completion", Ascending = true });

            // Set defaults
            SelectedFilterType = _filterTypes.FirstOrDefault();
            SelectedFilterPhase = _filterPhases.FirstOrDefault();
            SelectedSortOption = _sortOptions.FirstOrDefault();
        }

        private void ApplyFiltersAndSort()
        {
            var query = _allProjects.AsEnumerable();

            // Apply type filter
            if (SelectedFilterType?.Value != null)
            {
                query = query.Where(p => p.Type == SelectedFilterType.Value);
            }

            // Apply phase filter
            if (SelectedFilterPhase?.Value != null)
            {
                query = query.Where(p => p.Phase == SelectedFilterPhase.Value);
            }

            // Apply sorting
            if (SelectedSortOption != null)
            {
                query = SelectedSortOption.SortKey switch
                {
                    "Name" => SelectedSortOption.Ascending
                        ? query.OrderBy(p => p.Name)
                        : query.OrderByDescending(p => p.Name),
                    "Priority" => SelectedSortOption.Ascending
                        ? query.OrderBy(p => p.Priority)
                        : query.OrderByDescending(p => p.Priority),
                    "Deadline" => SelectedSortOption.Ascending
                        ? query.OrderBy(p => p.Deadline)
                        : query.OrderByDescending(p => p.Deadline),
                    "Completion" => SelectedSortOption.Ascending
                        ? query.OrderBy(p => p.CompletionPercentage)
                        : query.OrderByDescending(p => p.CompletionPercentage),
                    _ => query.OrderBy(p => p.Name)
                };
            }

            // Update filtered collection
            var app = Application.Current;
            if (app?.Dispatcher != null)
            {
                app.Dispatcher.Invoke(() =>
                {
                    FilteredProjects.Clear();
                    foreach (var project in query)
                    {
                        FilteredProjects.Add(project);
                    }
                });
            }
        }

        public async Task LoadProjectsAsync()
        {
            try
            {
                var projects = await _projectManagementService.GetAllProjectsAsync();
                
                if (projects == null)
                {
                    System.Diagnostics.Debug.WriteLine("GetAllProjectsAsync returned null");
                    return;
                }
                
                var app = Application.Current;
                if (app == null || app.Dispatcher == null)
                {
                    System.Diagnostics.Debug.WriteLine("Application or Dispatcher is null, cannot update UI");
                    return;
                }
                
                await app.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        _allProjects.Clear();
                        foreach (var project in projects)
                        {
                            if (project != null)
                            {
                                try
                                {
                                    _allProjects.Add(new ProjectViewModel(project));
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error creating ProjectViewModel for project '{project.Name}': {ex.Message}");
                                    // Continue with next project instead of crashing
                                }
                            }
                        }
                        
                        // Also update Projects collection for backward compatibility
                        Projects.Clear();
                        foreach (var projectViewModel in _allProjects)
                        {
                            Projects.Add(projectViewModel);
                        }
                        
                        // Apply filters and sort
                        ApplyFiltersAndSort();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error adding projects to collection: {ex.Message}\n{ex.StackTrace}");
                        // Don't show MessageBox here as it might cause issues during window initialization
                    }
                }, System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading projects: {ex.Message}\n{ex.StackTrace}");
                // Try to show error on UI thread if possible
                try
                {
                    var app = Application.Current;
                    if (app != null && app.Dispatcher != null)
                    {
                        await app.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show($"Error loading projects: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public async Task OpenProjectDetailAsync(string projectId)
        {
            try
            {
                var project = await _projectManagementService.GetProjectAsync(projectId);
                if (project == null)
                {
                    MessageBox.Show("Project not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var persistenceService = _persistenceService ?? App.GetService<IPersistenceService>();
                var dialog = new ProjectDetailDialog(project, _projectManagementService, persistenceService);
                dialog.Owner = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                
                dialog.ShowDialog();
                
                // Refresh if project was updated or deleted
                if (dialog.ProjectWasUpdated || dialog.ProjectWasDeleted)
                {
                    await LoadProjectsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening project details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in OpenProjectDetailAsync: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task CreateProjectAsync()
        {
            try
            {
                var dialog = new CreateProjectDialog();
                dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;
                
                var result = dialog.ShowDialog();
                
                if (result == true && dialog.CreatedProject != null)
                {
                    // Refresh project list
                    await LoadProjectsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in CreateProjectAsync: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    public class ProjectViewModel : ObservableObject
    {
        private readonly Project _project;

        public ProjectViewModel(Project project)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
        }

        public string Id => _project?.Id ?? string.Empty;
        public string Name => _project?.Name ?? string.Empty;
        public string Description => _project?.Description ?? string.Empty;
        public ProjectType Type => _project?.Type ?? ProjectType.Other;
        public string TypeString => _project?.Type.ToString() ?? "Other";
        public ProjectPhase Phase => _project?.Phase ?? ProjectPhase.Planning;
        public string PhaseString => _project?.Phase.ToString() ?? "Planning";
        public int Priority => _project?.Priority ?? 5;
        public DateTime Deadline => _project?.Deadline ?? DateTime.Now.AddDays(30);
        public double CompletionPercentage => _project?.CompletionPercentage ?? 0.0;

        public System.Windows.Media.Brush PriorityColor
        {
            get
            {
                if (_project == null)
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88)); // Gray default
                
                // Color gradient based on priority (1-10)
                // Lower priority (1-3): Red/Orange
                // Medium priority (4-7): Yellow/Orange
                // High priority (8-10): Green
                var priority = _project.Priority;
                if (priority <= 3)
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B)); // Red
                else if (priority <= 7)
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xA5, 0x00)); // Orange
                else
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50)); // Green
            }
        }

        public bool HasRoadblocks => _project?.Roadblocks != null && _project.Roadblocks.Count > 0;

        public string RoadblocksFormatted
        {
            get
            {
                if (_project == null || _project.Roadblocks == null || _project.Roadblocks.Count == 0)
                    return string.Empty;

                if (_project.Roadblocks.Count == 1)
                    return $"⚠️ {_project.Roadblocks[0]}";

                return $"⚠️ {_project.Roadblocks.Count} roadblocks";
            }
        }
    }

    // Helper classes for filtering and sorting
    public class FilterOption<T>
    {
        public string Name { get; set; } = string.Empty;
        public T? Value { get; set; }
    }

    public class SortOption
    {
        public string Name { get; set; } = string.Empty;
        public string SortKey { get; set; } = string.Empty;
        public bool Ascending { get; set; } = true;
    }
}
