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
    public class CreateProjectDialogViewModel : ObservableObject
    {
        private readonly IProjectManagementService _projectManagementService;
        private readonly IPersistenceService _persistenceService;

        // Form fields
        private string _projectName = string.Empty;
        private ProjectType _selectedType = ProjectType.Other;
        private string _description = string.Empty;
        private int _priority = 5;
        private DateTime _startDate = DateTime.Now;
        private DateTime _deadline = DateTime.Now.AddDays(30);
        private ProjectPhase _selectedPhase = ProjectPhase.Planning;
        private AIContactOption? _selectedAIContact;
        private ObservableCollection<string> _roadblocks = new();
        private string _newRoadblockText = string.Empty;
        private string? _validationError;

        // Collections for dropdowns
        private readonly ObservableCollection<ProjectType> _projectTypes = new();
        private readonly ObservableCollection<ProjectPhase> _projectPhases = new();
        private readonly ObservableCollection<AIContact> _aiContacts = new();
        private readonly ObservableCollection<AIContactOption> _aiContactsWithNone = new();

        public ObservableCollection<ProjectType> ProjectTypes => _projectTypes;
        public ObservableCollection<ProjectPhase> ProjectPhases => _projectPhases;
        public ObservableCollection<AIContact> AIContacts => _aiContacts;
        public ObservableCollection<AIContactOption> AIContactsWithNone => _aiContactsWithNone;
        public ObservableCollection<string> Roadblocks => _roadblocks;

        // Commands
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddRoadblockCommand { get; }
        public ICommand RemoveRoadblockCommand { get; }

        // Properties
        public string ProjectName
        {
            get => _projectName;
            set
            {
                if (SetProperty(ref _projectName, value))
                {
                    ValidateForm();
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ProjectType SelectedType
        {
            get => _selectedType;
            set => SetProperty(ref _selectedType, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public int Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    // Ensure deadline is not before start date
                    if (_deadline < _startDate)
                    {
                        Deadline = _startDate.AddDays(30);
                    }
                    ValidateForm();
                }
            }
        }

        public DateTime Deadline
        {
            get => _deadline;
            set
            {
                if (SetProperty(ref _deadline, value))
                {
                    ValidateForm();
                }
            }
        }

        public ProjectPhase SelectedPhase
        {
            get => _selectedPhase;
            set => SetProperty(ref _selectedPhase, value);
        }

        public AIContactOption? SelectedAIContact
        {
            get => _selectedAIContact;
            set => SetProperty(ref _selectedAIContact, value);
        }

        public string NewRoadblockText
        {
            get => _newRoadblockText;
            set => SetProperty(ref _newRoadblockText, value);
        }

        public string? ValidationError
        {
            get => _validationError;
            set => SetProperty(ref _validationError, value);
        }

        public bool IsValid => string.IsNullOrWhiteSpace(_validationError) && !string.IsNullOrWhiteSpace(_projectName?.Trim());

        public CreateProjectDialogViewModel(IProjectManagementService projectManagementService, IPersistenceService persistenceService)
        {
            _projectManagementService = projectManagementService ?? throw new ArgumentNullException(nameof(projectManagementService));
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));

            // Initialize enum collections
            foreach (ProjectType type in Enum.GetValues(typeof(ProjectType)))
            {
                _projectTypes.Add(type);
            }

            foreach (ProjectPhase phase in Enum.GetValues(typeof(ProjectPhase)))
            {
                _projectPhases.Add(phase);
            }

            // Initialize commands (Save and Cancel are handled in code-behind)
            SaveCommand = new RelayCommand(() => { }, () => false); // Not used, handled in code-behind
            CancelCommand = new RelayCommand(() => { }); // Not used, handled in code-behind
            AddRoadblockCommand = new RelayCommand(() => AddRoadblock(), () => !string.IsNullOrWhiteSpace(NewRoadblockText?.Trim()));
            RemoveRoadblockCommand = new RelayCommand(
                (parameter) => RemoveRoadblock(parameter as string), 
                (parameter) => !string.IsNullOrWhiteSpace(parameter as string));

            // Load AI contacts
            _ = LoadAIContactsAsync();
        }

        private async Task LoadAIContactsAsync()
        {
            try
            {
                var contacts = await _persistenceService.GetAllAsync<AIContact>();
                _aiContacts.Clear();
                _aiContactsWithNone.Clear();
                
                // Add "None" option first
                var noneOption = new AIContactOption { Id = null, Name = "None" };
                _aiContactsWithNone.Add(noneOption);
                
                foreach (var contact in contacts.Values)
                {
                    _aiContacts.Add(contact);
                    _aiContactsWithNone.Add(new AIContactOption { Id = contact.Id, Name = contact.Name });
                }
                
                // Set default selected AI to "None" after loading
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SelectedAIContact = noneOption;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading AI contacts: {ex.Message}");
            }
        }

        private void ValidateForm()
        {
            _validationError = null;

            if (string.IsNullOrWhiteSpace(_projectName?.Trim()))
            {
                _validationError = "Project name is required.";
                return;
            }

            if (_deadline < _startDate)
            {
                _validationError = "Deadline must be on or after the start date.";
                return;
            }

            if (_priority < 1 || _priority > 10)
            {
                _validationError = "Priority must be between 1 and 10.";
                return;
            }

            OnPropertyChanged(nameof(IsValid));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void AddRoadblock()
        {
            var trimmed = NewRoadblockText?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return;

            // Check for duplicates
            if (_roadblocks.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                ValidationError = "This roadblock already exists.";
                return;
            }

            _roadblocks.Add(trimmed);
            NewRoadblockText = string.Empty;
            ValidationError = null;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void RemoveRoadblock(string? roadblock)
        {
            if (string.IsNullOrWhiteSpace(roadblock))
                return;

            _roadblocks.Remove(roadblock);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        public async Task<Project?> SaveProjectAsync()
        {
            try
            {
                ValidateForm();
                if (!IsValid)
                {
                    return null;
                }

                var project = new Project
                {
                    Name = _projectName.Trim(),
                    Type = _selectedType,
                    Description = _description?.Trim() ?? string.Empty,
                    Priority = _priority,
                    StartDate = _startDate,
                    Deadline = _deadline,
                    Phase = _selectedPhase,
                    Roadblocks = _roadblocks.ToList(),
                    AssignedAIId = _selectedAIContact?.Id
                };

                var createdProject = await _projectManagementService.CreateProjectAsync(project);
                return createdProject;
            }
            catch (Exception ex)
            {
                ValidationError = $"Error creating project: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error creating project: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
    }

    // Helper class for AI contact dropdown with "None" option
    public class AIContactOption
    {
        public string? Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
