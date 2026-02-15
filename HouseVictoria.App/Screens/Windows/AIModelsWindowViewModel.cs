using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Windows;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace HouseVictoria.App.Screens.Windows
{
    public class AIModelsWindowViewModel : ObservableObject
    {
        private readonly IAIService _aiService;
        private readonly IPersistenceService _persistenceService;
        private readonly IMemoryService _memoryService;
        private readonly IMCPService? _mcpService;
        private readonly AppConfig _appConfig;
        private string _currentView = "ContactBook"; // ContactBook, LoadModel, CreatePersona
        private AIContact? _selectedContact;
        private string _selectedModel = string.Empty;
        private string _availableModelsEndpoint = "http://localhost:11434";
        private ObservableCollection<string> _availableModels = new();
        private ObservableCollection<AIContact> _aiContacts = new();

        // Create Persona fields
        private string _newPersonaName = string.Empty;
        private string _newPersonaModel = string.Empty;
        private string _newPersonaSystemPrompt = string.Empty;
        private string _newPersonaDescription = string.Empty;
        private string _newPersonaMCPServer = string.Empty;
        private string _newPersonaPiperVoice = string.Empty;
        private ObservableCollection<string> _availablePiperVoices = new();
        
        // LLM Parameters
        private double _newPersonaTemperature = 0.7;
        private double _newPersonaTopP = 0.9;
        private int _newPersonaTopK = 40;
        private double _newPersonaRepeatPenalty = 1.1;
        private int _newPersonaMaxTokens = -1;
        private int _newPersonaContextLength = 4096;
        
        // Pull Model fields
        private string _pullModelName = string.Empty;
        private string _pullModelEndpoint = "http://localhost:11434";
        private bool _isPullingModel = false;
        private string _pullModelStatus = string.Empty;
        
        // Ollama Run fields
        private string _ollamaRunCommand = string.Empty;
        private string _loadModelLog = string.Empty;
        private bool _isRunningOllamaCommand = false;
        private CancellationTokenSource? _ollamaCts;
        
        // Image Generation fields
        private string _imageGenerationPrompt = string.Empty;
        private int _imageWidth = 512;
        private int _imageHeight = 512;
        private bool _isGeneratingImage = false;
        private string _imageGenerationStatus = string.Empty;
        private string? _generatedImagePath;

        public ObservableCollection<string> AvailableModels => _availableModels;
        public ObservableCollection<AIContact> AIContacts => _aiContacts;

        public ICommand LoadModelCommand { get; }
        public ICommand CreatePersonaCommand { get; }
        public ICommand ShowContactBookCommand { get; }
        public ICommand LoadAvailableModelsCommand { get; }
        public ICommand SavePersonaCommand { get; }
        public ICommand DeletePersonaCommand { get; }
        public ICommand LoadPersonaCommand { get; }
        public ICommand EditPersonaCommand { get; }
        public ICommand PullModelCommand { get; }
        public ICommand PullModelForPersonaCommand { get; }
        public ICommand RunOllamaCommand { get; }
        public ICommand CancelOllamaCommand { get; }
        public ICommand ShowImageGenerationCommand { get; }
        public ICommand GenerateImageCommand { get; }
        public ICommand SaveGeneratedImageCommand { get; }

        public string CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public AIContact? SelectedContact
        {
            get => _selectedContact;
            set => SetProperty(ref _selectedContact, value);
        }

        public string SelectedModel
        {
            get => _selectedModel;
            set => SetProperty(ref _selectedModel, value);
        }

        public string AvailableModelsEndpoint
        {
            get => _availableModelsEndpoint;
            set => SetProperty(ref _availableModelsEndpoint, value);
        }

        public string NewPersonaName
        {
            get => _newPersonaName;
            set 
            { 
                if (SetProperty(ref _newPersonaName, value))
                {
                    // Trigger command re-evaluation
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string NewPersonaModel
        {
            get => _newPersonaModel;
            set 
            { 
                if (SetProperty(ref _newPersonaModel, value))
                {
                    // Trigger command re-evaluation
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string NewPersonaSystemPrompt
        {
            get => _newPersonaSystemPrompt;
            set => SetProperty(ref _newPersonaSystemPrompt, value);
        }

        public string NewPersonaDescription
        {
            get => _newPersonaDescription;
            set => SetProperty(ref _newPersonaDescription, value);
        }

        public string NewPersonaMCPServer
        {
            get => _newPersonaMCPServer;
            set => SetProperty(ref _newPersonaMCPServer, value);
        }

        public double NewPersonaTemperature
        {
            get => _newPersonaTemperature;
            set => SetProperty(ref _newPersonaTemperature, value);
        }

        public double NewPersonaTopP
        {
            get => _newPersonaTopP;
            set => SetProperty(ref _newPersonaTopP, value);
        }

        public int NewPersonaTopK
        {
            get => _newPersonaTopK;
            set => SetProperty(ref _newPersonaTopK, value);
        }

        public double NewPersonaRepeatPenalty
        {
            get => _newPersonaRepeatPenalty;
            set => SetProperty(ref _newPersonaRepeatPenalty, value);
        }

        public int NewPersonaMaxTokens
        {
            get => _newPersonaMaxTokens;
            set => SetProperty(ref _newPersonaMaxTokens, value);
        }

        public int NewPersonaContextLength
        {
            get => _newPersonaContextLength;
            set => SetProperty(ref _newPersonaContextLength, value);
        }

        public string NewPersonaPiperVoice
        {
            get => _newPersonaPiperVoice;
            set => SetProperty(ref _newPersonaPiperVoice, value);
        }

        public ObservableCollection<string> AvailablePiperVoices => _availablePiperVoices;

        public string PullModelName
        {
            get => _pullModelName;
            set => SetProperty(ref _pullModelName, value);
        }

        public string PullModelEndpoint
        {
            get => _pullModelEndpoint;
            set => SetProperty(ref _pullModelEndpoint, value);
        }

        public bool IsPullingModel
        {
            get => _isPullingModel;
            set
            {
                if (SetProperty(ref _isPullingModel, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string PullModelStatus
        {
            get => _pullModelStatus;
            set => SetProperty(ref _pullModelStatus, value);
        }

        public string OllamaRunCommand
        {
            get => _ollamaRunCommand;
            set => SetProperty(ref _ollamaRunCommand, value);
        }

        public string LoadModelLog
        {
            get => _loadModelLog;
            set => SetProperty(ref _loadModelLog, value);
        }

        public bool IsRunningOllamaCommand
        {
            get => _isRunningOllamaCommand;
            set
            {
                if (SetProperty(ref _isRunningOllamaCommand, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                    OnPropertyChanged(nameof(CanStopOllamaCommand));
                }
            }
        }

        public bool CanStopOllamaCommand => _isRunningOllamaCommand;

        // Image Generation Properties
        public string ImageGenerationPrompt
        {
            get => _imageGenerationPrompt;
            set
            {
                if (SetProperty(ref _imageGenerationPrompt, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public int ImageWidth
        {
            get => _imageWidth;
            set => SetProperty(ref _imageWidth, value);
        }

        public int ImageHeight
        {
            get => _imageHeight;
            set => SetProperty(ref _imageHeight, value);
        }

        public bool IsGeneratingImage
        {
            get => _isGeneratingImage;
            set
            {
                if (SetProperty(ref _isGeneratingImage, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ImageGenerationStatus
        {
            get => _imageGenerationStatus;
            set
            {
                SetProperty(ref _imageGenerationStatus, value);
                OnPropertyChanged(nameof(HasImageGenerationStatus));
            }
        }

        public bool HasImageGenerationStatus => !string.IsNullOrWhiteSpace(_imageGenerationStatus);

        public string? GeneratedImagePath
        {
            get => _generatedImagePath;
            set
            {
                SetProperty(ref _generatedImagePath, value);
                OnPropertyChanged(nameof(HasGeneratedImage));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasGeneratedImage => !string.IsNullOrWhiteSpace(_generatedImagePath);

        public AIModelsWindowViewModel(IAIService aiService, IPersistenceService persistenceService, IMemoryService memoryService, AppConfig appConfig, IMCPService? mcpService = null)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _mcpService = mcpService;

            LoadModelCommand = new RelayCommand(() => CurrentView = "LoadModel");
            CreatePersonaCommand = new RelayCommand(async () => 
            {
                CurrentView = "CreatePersona";
                // Load available models when switching to Create Persona view
                if (_availableModels.Count == 0)
                    await LoadAvailableModelsAsync();
                // Always reload Piper voices so dropdown shows current server/list or local Piper data dir
                await LoadAvailablePiperVoicesAsync();
            });
            ShowContactBookCommand = new RelayCommand(() => CurrentView = "ContactBook");
            LoadAvailableModelsCommand = new RelayCommand(async () => await LoadAvailableModelsAsync());
            SavePersonaCommand = new RelayCommand(async () => await CreatePersonaAsync(), () => !string.IsNullOrWhiteSpace(NewPersonaName) && !string.IsNullOrWhiteSpace(NewPersonaModel));
            DeletePersonaCommand = new RelayCommand(async (param) => await DeletePersonaAsync(param as AIContact), (param) => param is AIContact);
            LoadPersonaCommand = new RelayCommand(async (param) => await LoadPersonaAsync(param as AIContact), (param) => param is AIContact);
            EditPersonaCommand = new RelayCommand(async (param) => await EditPersonaAsync(param as AIContact), (param) => param is AIContact);
            ShowImageGenerationCommand = new RelayCommand(() => CurrentView = "ImageGeneration");
            GenerateImageCommand = new RelayCommand(async () => await GenerateImageAsync(), () => !string.IsNullOrWhiteSpace(ImageGenerationPrompt) && !IsGeneratingImage);
            SaveGeneratedImageCommand = new RelayCommand(async () => await SaveGeneratedImageAsync(), () => !string.IsNullOrWhiteSpace(GeneratedImagePath));
            PullModelCommand = new RelayCommand(async () => await PullModelAsync(), () => !string.IsNullOrWhiteSpace(PullModelName) && !IsPullingModel);
            PullModelForPersonaCommand = new RelayCommand(async () => await PullModelForPersonaAsync(), () => !string.IsNullOrWhiteSpace(NewPersonaModel) && !IsPullingModel);
            RunOllamaCommand = new RelayCommand(async () => await RunOllamaCommandAsync(), () => !string.IsNullOrWhiteSpace(OllamaRunCommand) && !IsPullingModel && !IsRunningOllamaCommand);
            CancelOllamaCommand = new RelayCommand(() => CancelOllamaPull(), () => IsRunningOllamaCommand);

            AvailableModelsEndpoint = _appConfig.OllamaEndpoint;
            PullModelEndpoint = _appConfig.OllamaEndpoint;
            NewPersonaMCPServer = _appConfig.MCPServerEndpoint;

            _ = LoadAIContactsAsync();
        }

        private async Task LoadAIContactsAsync()
        {
            try
            {
                var contacts = await _persistenceService.GetAllAsync<AIContact>();
                _aiContacts.Clear();
                foreach (var contact in contacts.Values)
                {
                    _aiContacts.Add(contact);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading AI contacts: {ex.Message}");
            }
        }

        private async Task LoadAvailableModelsAsync()
        {
            try
            {
                var models = await _aiService.GetAvailableModelsAsync(AvailableModelsEndpoint);
                _availableModels.Clear();
                foreach (var model in models)
                    _availableModels.Add(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading available models: {ex.Message}");
            }
        }

        private async Task LoadAvailablePiperVoicesAsync()
        {
            try
            {
                var ttsService = App.ServiceProvider?.GetService<ITTSService>();
                _availablePiperVoices.Clear();
                if (ttsService != null)
                {
                    var voices = await ttsService.GetAvailablePiperVoicesAsync();
                    foreach (var v in voices)
                        _availablePiperVoices.Add(v);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading Piper voices: {ex.Message}");
            }
        }

        private async Task CreatePersonaAsync()
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(NewPersonaName))
                {
                    System.Diagnostics.Debug.WriteLine("Error: Persona name is required");
                    PullModelStatus = "✗ Error: Persona name is required";
                    return;
                }

                if (string.IsNullOrWhiteSpace(NewPersonaModel))
                {
                    System.Diagnostics.Debug.WriteLine("Error: Model name is required");
                    PullModelStatus = "✗ Error: Model name is required";
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Creating persona: Name={NewPersonaName}, Model={NewPersonaModel}");

                // Check for duplicate names
                var trimmedName = NewPersonaName.Trim();
                if (_aiContacts.Any(c => c.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)))
                {
                    var errorMsg = $"An AI contact with the name '{trimmedName}' already exists. Please choose a different name.";
                    System.Diagnostics.Debug.WriteLine($"Error: {errorMsg}");
                    PullModelStatus = $"✗ {errorMsg}";
                    return;
                }

                // Generate unique MCP server endpoint for this persona
                var personaId = Guid.NewGuid().ToString();
                var mcpEndpoint = string.IsNullOrWhiteSpace(NewPersonaMCPServer) 
                    ? $"http://localhost:{8080 + _aiContacts.Count}" 
                    : NewPersonaMCPServer;

                // Create data path for this persona
                var dataPath = System.IO.Path.Combine(_appConfig.DataBankPath, personaId);
                try
                {
                    // Ensure the base DataBankPath directory exists
                    if (!System.IO.Directory.Exists(_appConfig.DataBankPath))
                    {
                        System.IO.Directory.CreateDirectory(_appConfig.DataBankPath);
                        System.Diagnostics.Debug.WriteLine($"Created DataBankPath directory: {_appConfig.DataBankPath}");
                    }
                    
                    // Create the persona-specific directory
                    System.IO.Directory.CreateDirectory(dataPath);
                    System.Diagnostics.Debug.WriteLine($"Created persona data directory: {dataPath}");
                }
                catch (Exception dirEx)
                {
                    var errorMsg = $"Failed to create data directory: {dirEx.Message}";
                    System.Diagnostics.Debug.WriteLine($"{errorMsg}\n{dirEx.StackTrace}");
                    PullModelStatus = $"✗ {errorMsg}";
                    return;
                }

                // Create new AI contact
                var newContact = new AIContact
                {
                    Id = personaId,
                    Name = NewPersonaName.Trim(),
                    ModelName = NewPersonaModel.Trim(),
                    PiperVoiceId = string.IsNullOrWhiteSpace(NewPersonaPiperVoice) ? null : NewPersonaPiperVoice.Trim(),
                    SystemPrompt = NewPersonaSystemPrompt?.Trim(),
                    Description = NewPersonaDescription?.Trim(),
                    ServerEndpoint = AvailableModelsEndpoint,
                    MCPServerEndpoint = mcpEndpoint,
                    DataPath = dataPath,
                    CreatedAt = DateTime.Now,
                    LastUsedAt = DateTime.Now,
                    // LLM Parameters
                    Temperature = NewPersonaTemperature,
                    TopP = NewPersonaTopP,
                    TopK = NewPersonaTopK,
                    RepeatPenalty = NewPersonaRepeatPenalty,
                    MaxTokens = NewPersonaMaxTokens,
                    ContextLength = NewPersonaContextLength
                };

                System.Diagnostics.Debug.WriteLine($"Saving persona to persistence: {newContact.Id}");

                // Save to persistence
                await _persistenceService.SetAsync($"AIContact_{newContact.Id}", newContact);

                System.Diagnostics.Debug.WriteLine("Initializing MCP server for persona");

                // Initialize MCP server for this persona
                await InitializeMCPServerForPersonaAsync(newContact);

                System.Diagnostics.Debug.WriteLine($"Persona created successfully: {newContact.Name}");

                // Reload contacts from persistence to ensure consistency
                await LoadAIContactsAsync();

                // Clear form
                NewPersonaName = string.Empty;
                NewPersonaModel = string.Empty;
                NewPersonaSystemPrompt = string.Empty;
                NewPersonaDescription = string.Empty;
                PullModelStatus = string.Empty;
                // Reset LLM parameters to defaults
                NewPersonaTemperature = 0.7;
                NewPersonaTopP = 0.9;
                NewPersonaTopK = 40;
                NewPersonaRepeatPenalty = 1.1;
                NewPersonaMaxTokens = -1;
                NewPersonaContextLength = 4096;
                NewPersonaPiperVoice = string.Empty;

                // Switch to contact book
                CurrentView = "ContactBook";
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error creating persona: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"{errorMsg}\n{ex.StackTrace}");
                PullModelStatus = $"✗ {errorMsg}";
            }
        }

        private async Task InitializeMCPServerForPersonaAsync(AIContact contact)
        {
            try
            {
                // Create initial memory entry for this persona
                await _memoryService.AddMemoryAsync(contact.Id, $"Persona created: {contact.Name} on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                // Create initial data bank for this persona
                var dataBank = new DataBank
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"{contact.Name} - Personal Data",
                    Description = $"Data bank for {contact.Name} persona",
                    CreatedAt = DateTime.Now,
                    LastModified = DateTime.Now
                };
                await _memoryService.AddDataBankAsync(dataBank);

                // Save persona configuration
                var configPath = System.IO.Path.Combine(contact.DataPath ?? "", "config.json");
                var config = new
                {
                    contact.Id,
                    contact.Name,
                    contact.ModelName,
                    contact.MCPServerEndpoint,
                    CreatedAt = contact.CreatedAt
                };
                await System.IO.File.WriteAllTextAsync(configPath, System.Text.Json.JsonSerializer.Serialize(config));

                // Initialize MCP server context if service is available and endpoint is configured
                if (_mcpService != null && !string.IsNullOrWhiteSpace(contact.MCPServerEndpoint))
                {
                    try
                    {
                        // Check if server is available
                        var isAvailable = await _mcpService.IsServerAvailableAsync(contact.MCPServerEndpoint);
                        if (isAvailable)
                        {
                            System.Diagnostics.Debug.WriteLine($"MCP server is available at {contact.MCPServerEndpoint}");

                            // Initialize context for this persona
                            var metadata = new Dictionary<string, object>
                            {
                                ["modelName"] = contact.ModelName,
                                ["description"] = contact.Description ?? string.Empty,
                                ["createdAt"] = contact.CreatedAt.ToString("O")
                            };

                            var contextInitialized = await _mcpService.InitializeContextAsync(
                                contact.MCPServerEndpoint,
                                contact.Id,
                                contact.Name,
                                metadata);

                            if (contextInitialized)
                            {
                                System.Diagnostics.Debug.WriteLine($"MCP context initialized for persona: {contact.Name}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to initialize MCP context for persona: {contact.Name}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"MCP server is not available at {contact.MCPServerEndpoint}");
                        }
                    }
                    catch (Exception mcpEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error connecting to MCP server: {mcpEx.Message}");
                        // Continue even if MCP initialization fails
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing MCP server for persona: {ex.Message}");
            }
        }

        private async Task DeletePersonaAsync(AIContact? contact)
        {
            if (contact == null) return;

            try
            {
                await _persistenceService.DeleteAsync($"AIContact_{contact.Id}");
                _aiContacts.Remove(contact);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting persona: {ex.Message}");
            }
        }

        private async Task LoadPersonaAsync(AIContact? contact)
        {
            if (contact == null) return;

            try
            {
                // Load the model for this persona
                await _aiService.LoadModelAsync(contact);
                contact.IsLoaded = true;
                contact.LastUsedAt = DateTime.Now;

                // Save updated contact
                await _persistenceService.SetAsync($"AIContact_{contact.Id}", contact);

                // Notify that persona is loaded (this will be handled by the SMS window)
                System.Diagnostics.Debug.WriteLine($"Persona loaded: {contact.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading persona: {ex.Message}");
            }
        }

        private async Task EditPersonaAsync(AIContact? contact)
        {
            if (contact == null) return;

            try
            {
                // Create a copy of the contact to edit
                var contactCopy = new AIContact
                {
                    Id = contact.Id,
                    Name = contact.Name,
                    ModelName = contact.ModelName,
                    PiperVoiceId = contact.PiperVoiceId,
                    SystemPrompt = contact.SystemPrompt,
                    Description = contact.Description,
                    AvatarUrl = contact.AvatarUrl,
                    PersonalityTraits = contact.PersonalityTraits != null ? new Dictionary<string, string>(contact.PersonalityTraits) : new Dictionary<string, string>(),
                    ServerEndpoint = contact.ServerEndpoint,
                    MCPServerEndpoint = contact.MCPServerEndpoint,
                    AdditionalServers = contact.AdditionalServers != null ? new Dictionary<string, string>(contact.AdditionalServers) : new Dictionary<string, string>(),
                    IsLoaded = contact.IsLoaded,
                    CreatedAt = contact.CreatedAt,
                    LastUsedAt = contact.LastUsedAt,
                    IsPrimaryAI = contact.IsPrimaryAI,
                    DataPath = contact.DataPath,
                    // LLM Parameters
                    Temperature = contact.Temperature,
                    TopP = contact.TopP,
                    TopK = contact.TopK,
                    RepeatPenalty = contact.RepeatPenalty,
                    MaxTokens = contact.MaxTokens,
                    ContextLength = contact.ContextLength
                };

                // Open edit dialog
                var dialog = new EditSystemPromptDialog(contactCopy);
                try
                {
                    var app = System.Windows.Application.Current;
                    if (app != null)
                        dialog.Owner = app.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive) ?? app.MainWindow;
                }
                catch
                {
                    // Owner is optional; dialog can still show without it
                }
                var result = dialog.ShowDialog();

                // If user clicked Save, update the contact
                if (result == true)
                {
                    contact.SystemPrompt = dialog.SystemPrompt?.Trim();
                    contact.PiperVoiceId = string.IsNullOrWhiteSpace(dialog.PiperVoiceId) ? null : dialog.PiperVoiceId.Trim();
                    
                    // Save updated contact to persistence
                    await _persistenceService.SetAsync($"AIContact_{contact.Id}", contact);
                    
                    // Reload contacts to refresh UI
                    await LoadAIContactsAsync();
                    
                    System.Diagnostics.Debug.WriteLine($"System prompt updated for persona: {contact.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error editing persona: {ex.Message}");
                System.Windows.MessageBox.Show($"Error editing persona: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task PullModelAsync()
        {
            if (string.IsNullOrWhiteSpace(PullModelName))
                return;

            try
            {
                IsPullingModel = true;
                PullModelStatus = $"Pulling model '{PullModelName}'...";
                
                await _aiService.PullModelAsync(PullModelEndpoint, PullModelName);
                
                PullModelStatus = $"✓ Successfully pulled model '{PullModelName}'";
                
                // Refresh available models list
                await LoadAvailableModelsAsync();
                
                // Clear the input
                PullModelName = string.Empty;
            }
            catch (Exception ex)
            {
                PullModelStatus = $"✗ Error pulling model: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error pulling model: {ex.Message}");
            }
            finally
            {
                IsPullingModel = false;
            }
        }

        private async Task PullModelForPersonaAsync()
        {
            if (string.IsNullOrWhiteSpace(NewPersonaModel))
                return;

            try
            {
                IsPullingModel = true;
                PullModelStatus = $"Pulling model '{NewPersonaModel}'...";
                
                await _aiService.PullModelAsync(AvailableModelsEndpoint, NewPersonaModel);
                
                PullModelStatus = $"✓ Successfully pulled model '{NewPersonaModel}'";
                
                // Refresh available models list
                await LoadAvailableModelsAsync();
            }
            catch (Exception ex)
            {
                PullModelStatus = $"✗ Error pulling model: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error pulling model: {ex.Message}");
            }
            finally
            {
                IsPullingModel = false;
            }
        }

        private async Task RunOllamaCommandAsync()
        {
            if (string.IsNullOrWhiteSpace(OllamaRunCommand))
                return;

            var modelName = ExtractModelName(OllamaRunCommand);
            if (string.IsNullOrWhiteSpace(modelName))
            {
                PullModelStatus = "✗ Invalid command format. Please enter a model name (e.g., 'llama2' or 'ollama run llama2')";
                AppendLoadModelLog("Invalid command. Please enter a model name (e.g., 'llama2').");
                return;
            }

            _ollamaCts?.Cancel();
            _ollamaCts = new CancellationTokenSource();

            try
            {
                IsPullingModel = true;
                IsRunningOllamaCommand = true;
                PullModelStatus = $"Downloading model '{modelName}' via Ollama...";
                AppendLoadModelLog($"> ollama run {modelName}");

                using var client = new HttpClient
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"{PullModelEndpoint}/api/pull")
                {
                    Content = JsonContent.Create(new { name = modelName })
                };

                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _ollamaCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(_ollamaCts.Token);
                    var errorMessage = $"✗ Error downloading model: {response.StatusCode}";
                    PullModelStatus = errorMessage;
                    AppendLoadModelLog($"{errorMessage} - {errorContent}");
                    return;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(_ollamaCts.Token);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null && !_ollamaCts.IsCancellationRequested)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    AppendLoadModelLog(FormatOllamaStatus(line));
                }

                if (_ollamaCts.IsCancellationRequested)
                {
                    PullModelStatus = "Download cancelled.";
                    AppendLoadModelLog("Download cancelled.");
                    return;
                }

                PullModelStatus = $"✓ Successfully downloaded model '{modelName}'";
                AppendLoadModelLog("Download completed.");

                await LoadAvailableModelsAsync();

                // Clear the input
                OllamaRunCommand = string.Empty;
            }
            catch (OperationCanceledException)
            {
                PullModelStatus = "Download cancelled.";
                AppendLoadModelLog("Download cancelled.");
            }
            catch (Exception ex)
            {
                PullModelStatus = $"✗ Error downloading model: {ex.Message}";
                AppendLoadModelLog($"ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error running ollama command: {ex.Message}");
            }
            finally
            {
                IsPullingModel = false;
                IsRunningOllamaCommand = false;
                _ollamaCts?.Dispose();
                _ollamaCts = null;
            }
        }

        private void CancelOllamaPull()
        {
            _ollamaCts?.Cancel();
        }

        private string ExtractModelName(string command)
        {
            string modelName = command.Trim();

            if (modelName.StartsWith("ollama run", StringComparison.OrdinalIgnoreCase))
            {
                modelName = modelName.Substring("ollama run".Length).Trim();
            }
            else if (modelName.StartsWith("ollama", StringComparison.OrdinalIgnoreCase))
            {
                modelName = modelName.Substring("ollama".Length).Trim();
            }
            else if (modelName.StartsWith("run", StringComparison.OrdinalIgnoreCase))
            {
                modelName = modelName.Substring("run".Length).Trim();
            }

            var parts = modelName.Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                modelName = parts[0];
            }

            return modelName;
        }

        private string FormatOllamaStatus(string line)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() ?? string.Empty : string.Empty;
                var detail = root.TryGetProperty("detail", out var detailElement) ? detailElement.GetString() ?? string.Empty : string.Empty;

                if (root.TryGetProperty("digest", out var digestElement))
                {
                    var digest = digestElement.GetString();
                    if (!string.IsNullOrWhiteSpace(digest))
                    {
                        detail = string.IsNullOrWhiteSpace(detail) ? digest : $"{detail} ({digest})";
                    }
                }

                if (root.TryGetProperty("completed", out var completedElement) && root.TryGetProperty("total", out var totalElement))
                {
                    try
                    {
                        var completed = completedElement.GetInt64();
                        var total = totalElement.GetInt64();
                        if (total > 0)
                        {
                            detail = string.IsNullOrWhiteSpace(detail)
                                ? $"{completed}/{total}"
                                : $"{detail} {completed}/{total}";
                        }
                    }
                    catch
                    {
                        // Ignore parse errors for progress numbers
                    }
                }

                if (string.IsNullOrWhiteSpace(status) && string.IsNullOrWhiteSpace(detail))
                {
                    return line;
                }

                return string.IsNullOrWhiteSpace(detail) ? status : $"{status}: {detail}";
            }
            catch
            {
                return line;
            }
        }

        private void AppendLoadModelLog(string message)
        {
            var formatted = $"{DateTime.Now:HH:mm:ss} {message}";

            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LoadModelLog = string.IsNullOrEmpty(LoadModelLog)
                        ? formatted
                        : $"{LoadModelLog}{Environment.NewLine}{formatted}";
                });
            }
            else
            {
                LoadModelLog = string.IsNullOrEmpty(LoadModelLog)
                    ? formatted
                    : $"{LoadModelLog}{Environment.NewLine}{formatted}";
            }
        }

        private async Task GenerateImageAsync()
        {
            if (string.IsNullOrWhiteSpace(ImageGenerationPrompt))
            {
                ImageGenerationStatus = "Please enter a prompt to generate an image.";
                return;
            }

            string? resultPath = null;
            string resultStatus = "Generating image... Please wait.";
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsGeneratingImage = true;
                    ImageGenerationStatus = "Generating image... Please wait.";
                    GeneratedImagePath = null;
                });

                // Create a temporary AI contact for image generation (using default settings)
                var tempContact = new AIContact
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Image Generator",
                    ServerEndpoint = _appConfig.OllamaEndpoint ?? "http://localhost:11434",
                    ModelName = "flux" // Default image generation model, if available
                };

                // Generate image (long-running; may run on thread pool after await)
                using var imageStream = await _aiService.GenerateImageAsync(tempContact, ImageGenerationPrompt);

                // Save image to temporary file
                var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HouseVictoria", "GeneratedImages");
                System.IO.Directory.CreateDirectory(tempDir);
                var tempFileName = $"generated_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var tempFilePath = System.IO.Path.Combine(tempDir, tempFileName);

                using var fileStream = System.IO.File.Create(tempFilePath);
                await imageStream.CopyToAsync(fileStream);

                resultPath = tempFilePath;
                resultStatus = "✓ Image generated successfully!";
            }
            catch (NotImplementedException ex)
            {
                resultStatus = $"✗ Image generation is not available: {ex.Message}\n\n" +
                    "To enable image generation:\n" +
                    "1. Install Stable Diffusion (Automatic1111 webui) at http://localhost:7860, OR\n" +
                    "2. Set STABLE_DIFFUSION_ENDPOINT environment variable to your Stable Diffusion API endpoint";
            }
            catch (Exception ex)
            {
                resultStatus = $"✗ Error generating image: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error generating image: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                // Always update UI on dispatcher so "Generating image..." never gets stuck
                var path = resultPath;
                var status = resultStatus;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsGeneratingImage = false;
                    ImageGenerationStatus = status;
                    GeneratedImagePath = path;
                });
            }
        }

        private async Task SaveGeneratedImageAsync()
        {
            if (string.IsNullOrWhiteSpace(GeneratedImagePath))
                return;

            try
            {
                var fileGenerationService = App.GetService<IFileGenerationService>();
                if (fileGenerationService == null)
                {
                    System.Windows.MessageBox.Show("File generation service is not available.", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                // Get file path (may be URI or direct path)
                var sourcePath = GeneratedImagePath;
                if (GeneratedImagePath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(GeneratedImagePath);
                    sourcePath = uri.LocalPath;
                }
                
                if (!System.IO.File.Exists(sourcePath))
                {
                    System.Windows.MessageBox.Show("Generated image file not found.", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                // Read image bytes
                var imageBytes = await System.IO.File.ReadAllBytesAsync(sourcePath);
                
                // Generate filename with timestamp
                var fileName = $"generated_image_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                
                // Save using FileGenerationService
                var savedPath = await fileGenerationService.CreateFileAsync(fileName, imageBytes, "Images");
                
                System.Windows.MessageBox.Show($"Image saved successfully!\n\nSaved to: {savedPath}", "Success", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                
                ImageGenerationStatus = $"✓ Image saved to: {savedPath}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving image: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error saving generated image: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
