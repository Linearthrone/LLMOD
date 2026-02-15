using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Utils;
using HouseVictoria.Core.Models;
using System.Windows;

namespace HouseVictoria.App.Screens.Windows
{
    public class SMSMMSWindowViewModel : ObservableObject
    {
        private readonly ICommunicationService _communicationService;
        private readonly IEventAggregator _eventAggregator;
        private string _messageText = string.Empty;
        private Conversation? _selectedConversation;
        private Contact? _selectedContact;
        private bool _showConversationList = true;
        private bool _showContactSelection = false;
        private bool _showAppsView = false;
        private bool _showDialerView = false;
        private Contact? _selectedDialerContact = null;
        private CallState _currentCallState = CallState.None;
        private string? _loadingConversationId = null; // Track which conversation is currently loading to prevent concurrent loads

        public ObservableCollection<ConversationViewModel> Conversations { get; }
        public ObservableCollection<ConversationMessage> Messages { get; }
        public ObservableCollection<Contact> Contacts { get; }

        public ICommand SendMessageCommand { get; }
        public ICommand SelectConversationCommand { get; }
        public ICommand BackToConversationListCommand { get; }
        public ICommand StartNewConversationCommand { get; }
        public ICommand SelectContactCommand { get; }
        public ICommand AttachMediaCommand { get; }
        public ICommand ClearMediaCommand { get; }
        public ICommand ToggleAudioRecordingCommand { get; }
        public ICommand StartCallCommand { get; }
        public ICommand EndCallCommand { get; }
        public ICommand ToggleAppsViewCommand { get; }
        public ICommand OpenMessagesAppCommand { get; }
        public ICommand OpenPhoneAppCommand { get; }
        public ICommand SelectDialerContactCommand { get; }
        public ICommand StartDialerCallCommand { get; }
        public ICommand EndDialerCallCommand { get; }
        public ICommand BackFromDialerCommand { get; }

        // Media attachment state
        private string? _pendingMediaPath;
        private MessageType _pendingMediaType = MessageType.Text;
        private string? _pendingMediaFileName;
        private long _pendingMediaFileSize;

        public string? PendingMediaPath
        {
            get => _pendingMediaPath;
            set
            {
                if (SetProperty(ref _pendingMediaPath, value))
                {
                    OnPropertyChanged(nameof(HasPendingMedia));
                    OnPropertyChanged(nameof(PendingMediaDisplayInfo));
                }
            }
        }

        public MessageType PendingMediaType
        {
            get => _pendingMediaType;
            set
            {
                if (SetProperty(ref _pendingMediaType, value))
                {
                    OnPropertyChanged(nameof(PendingMediaDisplayInfo));
                }
            }
        }

        public string? PendingMediaFileName
        {
            get => _pendingMediaFileName;
            set
            {
                if (SetProperty(ref _pendingMediaFileName, value))
                {
                    OnPropertyChanged(nameof(PendingMediaDisplayInfo));
                }
            }
        }

        public long PendingMediaFileSize
        {
            get => _pendingMediaFileSize;
            set
            {
                if (SetProperty(ref _pendingMediaFileSize, value))
                {
                    OnPropertyChanged(nameof(PendingMediaDisplayInfo));
                    OnPropertyChanged(nameof(PendingMediaFileSizeDisplay));
                }
            }
        }

        public string PendingMediaFileSizeDisplay
        {
            get
            {
                if (_pendingMediaFileSize < 1024)
                    return $"{_pendingMediaFileSize} B";
                else if (_pendingMediaFileSize < 1024 * 1024)
                    return $"{_pendingMediaFileSize / 1024.0:F1} KB";
                else
                    return $"{_pendingMediaFileSize / (1024.0 * 1024.0):F1} MB";
            }
        }

        public string? PendingMediaDisplayInfo
        {
            get
            {
                if (!HasPendingMedia)
                    return null;

                var typeIcon = PendingMediaType switch
                {
                    MessageType.Image => "📷",
                    MessageType.Video => "🎥",
                    MessageType.Audio => "🎵",
                    MessageType.Document => "📄",
                    _ => "📎"
                };

                return $"{typeIcon} {PendingMediaFileName} ({PendingMediaFileSizeDisplay})";
            }
        }

        public bool HasPendingMedia => !string.IsNullOrWhiteSpace(_pendingMediaPath) && File.Exists(_pendingMediaPath);

        // Audio recording state
        private bool _isRecordingAudio = false;
        private string? _recordedAudioPath;
        private System.Threading.Timer? _recordingTimer;
        private TimeSpan _recordingDuration = TimeSpan.Zero;

        public bool IsRecordingAudio
        {
            get => _isRecordingAudio;
            private set
            {
                if (SetProperty(ref _isRecordingAudio, value))
                {
                    OnPropertyChanged(nameof(AudioRecordingTooltip));
                }
            }
        }

        public string AudioRecordingTooltip => _isRecordingAudio ? "Stop recording" : "Start voice recording";

        public Conversation? SelectedConversation
        {
            get => _selectedConversation;
            set
            {
                if (SetProperty(ref _selectedConversation, value) && value != null)
                {
                    LoadMessages(value.Id);
                    _showConversationList = false;
                    _showAppsView = false;
                    _showDialerView = false;
                    OnPropertyChanged(nameof(ShowConversationList));
                    OnPropertyChanged(nameof(ShowMessageView));
                    OnPropertyChanged(nameof(ShowAppsView));
                    OnPropertyChanged(nameof(ShowDialerView));
                }
            }
        }

        public Contact? SelectedContact
        {
            get => _selectedContact;
            set => SetProperty(ref _selectedContact, value);
        }

        public string MessageText
        {
            get => _messageText;
            set => SetProperty(ref _messageText, value);
        }

        public bool ShowConversationList
        {
            get => _showConversationList;
            set
            {
                if (SetProperty(ref _showConversationList, value))
                {
                    OnPropertyChanged(nameof(ShowMessageView));
                }
            }
        }

        public bool ShowMessageView => !_showConversationList && !_showContactSelection && !_showAppsView && !_showDialerView && _selectedConversation != null;
        
        public bool ShowAppsView
        {
            get => _showAppsView;
            set
            {
                if (SetProperty(ref _showAppsView, value))
                {
                    OnPropertyChanged(nameof(ShowConversationList));
                    OnPropertyChanged(nameof(ShowMessageView));
                    OnPropertyChanged(nameof(ShowDialerView));
                }
            }
        }
        
        public bool ShowDialerView
        {
            get => _showDialerView;
            set
            {
                if (SetProperty(ref _showDialerView, value))
                {
                    OnPropertyChanged(nameof(ShowConversationList));
                    OnPropertyChanged(nameof(ShowMessageView));
                    OnPropertyChanged(nameof(ShowAppsView));
                }
            }
        }
        
        public Contact? SelectedDialerContact
        {
            get => _selectedDialerContact;
            set
            {
                if (SetProperty(ref _selectedDialerContact, value))
                {
                    OnPropertyChanged(nameof(CanStartDialerCall));
                    OnPropertyChanged(nameof(IsDialerContactSelected));
                }
            }
        }
        
        public bool CanStartDialerCall => _selectedDialerContact != null && !IsCallActive;
        public bool CanEndDialerCall => IsCallActive && _showDialerView;
        public bool IsDialerContactSelected => _selectedDialerContact != null;

        public CallState CurrentCallState
        {
            get => _currentCallState;
            set
            {
                if (SetProperty(ref _currentCallState, value))
                {
                    OnPropertyChanged(nameof(IsCallActive));
                    OnPropertyChanged(nameof(CanStartCall));
                    OnPropertyChanged(nameof(CanEndCall));
                    OnPropertyChanged(nameof(CanStartDialerCall));
                    OnPropertyChanged(nameof(CanEndDialerCall));
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsCallActive => _currentCallState == CallState.Connected || _currentCallState == CallState.Outgoing || _currentCallState == CallState.Incoming;
        public bool CanStartCall => _selectedConversation != null && !IsCallActive;
        public bool CanEndCall => IsCallActive;
        public bool ShowContactSelection
        {
            get => _showContactSelection;
            set
            {
                if (SetProperty(ref _showContactSelection, value))
                {
                    OnPropertyChanged(nameof(ShowConversationList));
                    OnPropertyChanged(nameof(ShowMessageView));
                    OnPropertyChanged(nameof(ShowAppsView));
                    OnPropertyChanged(nameof(ShowDialerView));
                }
            }
        }

        public SMSMMSWindowViewModel(ICommunicationService communicationService, IEventAggregator? eventAggregator = null)
        {
            _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
            _eventAggregator = eventAggregator ?? new EventAggregator();
            Conversations = new ObservableCollection<ConversationViewModel>();
            Messages = new ObservableCollection<ConversationMessage>();
            Contacts = new ObservableCollection<Contact>();

            SendMessageCommand = new RelayCommand(async () => await SendMessageAsync(), () => 
                (!string.IsNullOrWhiteSpace(MessageText) || HasPendingMedia) && _selectedConversation != null);
            SelectConversationCommand = new RelayCommand(async (parameter) => 
            {
                if (parameter is ConversationViewModel convVm)
                {
                    await SelectConversationAsync(convVm.Conversation);
                }
            });
            BackToConversationListCommand = new RelayCommand(() => 
            {
                ShowConversationList = true;
                ShowContactSelection = false;
                ShowAppsView = false;
                ShowDialerView = false;
                SelectedConversation = null;
            });
            StartNewConversationCommand = new RelayCommand(() => 
            {
                ShowContactSelection = true;
                ShowConversationList = false;
                ShowAppsView = false;
                ShowDialerView = false;
            });
            SelectContactCommand = new RelayCommand(async (parameter) => 
            {
                if (parameter is Contact contact)
                {
                    await StartConversationWithContactAsync(contact);
                }
            });
            AttachMediaCommand = new RelayCommand(() => AttachMedia());
            ClearMediaCommand = new RelayCommand(() => ClearPendingMedia(), () => HasPendingMedia);
            ToggleAudioRecordingCommand = new RelayCommand(async () => await ToggleAudioRecordingAsync());
            StartCallCommand = new RelayCommand(async () => await StartCallAsync(), () => CanStartCall);
            EndCallCommand = new RelayCommand(async () => await EndCallAsync(), () => CanEndCall);
            ToggleAppsViewCommand = new RelayCommand(() => 
            {
                ShowAppsView = !ShowAppsView;
                if (ShowAppsView)
                {
                    ShowConversationList = false;
                    ShowContactSelection = false;
                    ShowDialerView = false;
                }
            });
            OpenMessagesAppCommand = new RelayCommand(() => 
            {
                ShowAppsView = false;
                ShowConversationList = true;
                ShowDialerView = false;
            });
            OpenPhoneAppCommand = new RelayCommand(() => 
            {
                ShowAppsView = false;
                ShowDialerView = true;
                ShowConversationList = false;
                SelectedDialerContact = null;
            });
            SelectDialerContactCommand = new RelayCommand((parameter) => 
            {
                if (parameter is Contact contact)
                {
                    SelectedDialerContact = contact;
                }
            });
            StartDialerCallCommand = new RelayCommand(async () => await StartDialerCallAsync(), () => CanStartDialerCall);
            EndDialerCallCommand = new RelayCommand(async () => await EndDialerCallAsync(), () => CanEndDialerCall);
            BackFromDialerCommand = new RelayCommand(() => 
            {
                ShowDialerView = false;
                ShowAppsView = true;
                SelectedDialerContact = null;
            });

            // Subscribe to message received events
            _communicationService.MessageReceived += CommunicationService_MessageReceived;
            _communicationService.CallStateChanged += CommunicationService_CallStateChanged;

            // Load initial data
            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            try
            {
                // Load contacts first
                var contacts = await _communicationService.GetContactsAsync();
                Contacts.Clear();
                foreach (var contact in contacts)
                {
                    Contacts.Add(contact);
                }
                
                // Then load conversations (which need contacts)
                var conversations = await _communicationService.GetConversationsAsync().ConfigureAwait(false);
                
                // Load last messages in parallel to avoid blocking
                var conversationTasks = conversations.OrderByDescending(c => c.LastMessageAt).Select(async conv =>
                {
                    var contact = Contacts.FirstOrDefault(c => c.Id == conv.ContactId);
                    
                    // Get only the last message for preview (much faster than loading all messages)
                    ConversationMessage? lastMessage = null;
                    try
                    {
                        lastMessage = await _communicationService.GetLastMessageAsync(conv.Id).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading last message for conversation {conv.Id}: {ex.Message}");
                    }
                    
                    return new ConversationViewModel(conv, contact, lastMessage);
                });
                
                var conversationViewModels = await Task.WhenAll(conversationTasks).ConfigureAwait(false);
                
                // Update UI on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Conversations.Clear();
                    foreach (var convVm in conversationViewModels)
                    {
                        Conversations.Add(convVm);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
            }
        }

        private async void LoadMessages(string conversationId)
        {
            // Prevent concurrent loads for the same conversation
            if (_loadingConversationId == conversationId)
            {
                System.Diagnostics.Debug.WriteLine($"LoadMessages already in progress for conversation {conversationId}. Skipping duplicate load.");
                return;
            }
            
            try
            {
                _loadingConversationId = conversationId;
                var messages = await _communicationService.GetMessagesAsync(conversationId).ConfigureAwait(false);
                
                // Update UI on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Get existing message IDs to check for duplicates - refresh this right before use to avoid race conditions
                    var existingMessageIds = new HashSet<string>(Messages.Select(m => m.Id));
                    
                    // Load last 100 messages, ordered chronologically, and deduplicate at source
                    var orderedMessages = messages
                        .GroupBy(m => m.Id) // Group by ID to remove duplicates
                        .Select(g => g.First()) // Take first occurrence of each ID
                        .OrderBy(m => m.Timestamp)
                        .TakeLast(100)
                        .ToList();
                    
                    // Merge: add messages from service that we don't already have
                    // This avoids clearing the collection and preserves messages added via event handler
                    // Re-check existing IDs right before adding to prevent race conditions
                    var messagesToAdd = orderedMessages.Where(m => 
                    {
                        // Double-check that message doesn't exist - refresh check right before adding
                        var exists = Messages.Any(existing => existing.Id == m.Id);
                        return !exists;
                    }).ToList();
                    
                    int messagesAdded = 0;
                    foreach (var msg in messagesToAdd)
                    {
                        // Final check right before adding to prevent duplicates
                        if (!Messages.Any(m => m.Id == msg.Id))
                        {
                            Messages.Add(msg);
                            messagesAdded++;
                        }
                    }
                    
                    // Remove any messages that don't belong to this conversation
                    var messagesToRemove = Messages.Where(m => m.ConversationId != conversationId).ToList();
                    int messagesRemoved = messagesToRemove.Count;
                    foreach (var msg in messagesToRemove)
                    {
                        Messages.Remove(msg);
                    }
                    
                    // Check if we need to re-sort by comparing IDs in order
                    var sortedMessages = Messages.OrderBy(m => m.Timestamp).ToList();
                    bool needsResort = sortedMessages.Count != Messages.Count;
                    if (!needsResort)
                    {
                        // Check if messages are in the correct order by comparing IDs
                        for (int i = 0; i < Messages.Count; i++)
                        {
                            if (Messages[i].Id != sortedMessages[i].Id)
                            {
                                needsResort = true;
                                break;
                            }
                        }
                    }
                    
                    if (needsResort)
                    {
                        Messages.Clear();
                        foreach (var msg in sortedMessages)
                        {
                            Messages.Add(msg);
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Loaded messages for conversation {conversationId}: Total={Messages.Count}, Added={messagesAdded}, Removed={messagesRemoved}, NeedsResort={needsResort}");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading messages: {ex.Message}");
            }
            finally
            {
                // Always clear loading flag, even if there's an error
                _loadingConversationId = null;
            }
        }

        private void AttachMedia()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Attach Media",
                    Filter = "All Files (*.*)|*.*|Images (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Videos (*.mp4;*.avi;*.mov;*.wmv)|*.mp4;*.avi;*.mov;*.wmv|Audio (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|Documents (*.pdf;*.doc;*.docx;*.txt)|*.pdf;*.doc;*.docx;*.txt",
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    var filePath = dialog.FileName;
                    var fileInfo = new FileInfo(filePath);

                    // Validate file exists
                    if (!fileInfo.Exists)
                    {
                        MessageBox.Show("The selected file no longer exists.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Validate file size (50MB maximum)
                    const long maxFileSize = 50 * 1024 * 1024; // 50MB
                    if (fileInfo.Length > maxFileSize)
                    {
                        var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
                        MessageBox.Show(
                            $"File size ({sizeMB:F1} MB) exceeds the maximum allowed size of 50 MB.\n\nPlease select a smaller file or compress the file before attaching.",
                            "File Too Large",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    // Check for zero-length files
                    if (fileInfo.Length == 0)
                    {
                        MessageBox.Show("The selected file is empty and cannot be attached.", "Empty File", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var extension = fileInfo.Extension.ToLower();

                    // Determine message type from file extension
                    MessageType messageType = extension switch
                    {
                        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => MessageType.Image,
                        ".mp4" or ".avi" or ".mov" or ".wmv" => MessageType.Video,
                        ".mp3" or ".wav" or ".ogg" => MessageType.Audio,
                        ".pdf" or ".doc" or ".docx" or ".txt" => MessageType.Document,
                        _ => MessageType.Document
                    };

                    PendingMediaPath = filePath;
                    PendingMediaType = messageType;
                    PendingMediaFileName = fileInfo.Name;
                    PendingMediaFileSize = fileInfo.Length;

                    // Update send button state and clear command state
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error attaching media: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error attaching media: {ex.Message}");
            }
        }

        private void ClearPendingMedia()
        {
            PendingMediaPath = null;
            PendingMediaType = MessageType.Text;
            PendingMediaFileName = null;
            PendingMediaFileSize = 0;
            
            // Explicitly notify property changes to ensure UI updates
            OnPropertyChanged(nameof(HasPendingMedia));
            OnPropertyChanged(nameof(PendingMediaDisplayInfo));
            
            // Update command states
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private async System.Threading.Tasks.Task SendMessageAsync()
        {
            if (_selectedConversation == null || (string.IsNullOrWhiteSpace(MessageText) && !HasPendingMedia))
                return;

            var messageText = MessageText ?? string.Empty;
            var messageType = MessageType.Text;
            byte[]? mediaData = null;
            string? mediaFilePath = null;
            string? mediaType = null;

            // Handle media attachment
            if (HasPendingMedia && !string.IsNullOrWhiteSpace(PendingMediaPath))
            {
                try
                {
                    var fileInfo = new FileInfo(PendingMediaPath);
                    
                    // Copy media to app's media storage directory
                    var mediaStorageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Media", _selectedConversation.Id);
                    Directory.CreateDirectory(mediaStorageDir);
                    
                    var storedFileName = $"{Guid.NewGuid()}{fileInfo.Extension}";
                    var storedFilePath = Path.Combine(mediaStorageDir, storedFileName);
                    
                    File.Copy(PendingMediaPath, storedFilePath, overwrite: true);
                    
                    // Ensure FilePath is absolute
                    mediaFilePath = Path.GetFullPath(storedFilePath);
                    
                    // Read media data (limit size to avoid memory issues - only read small files)
                    // For images, we can store in memory for faster display
                    if (fileInfo.Length <= 10 * 1024 * 1024 && (PendingMediaType == MessageType.Image || PendingMediaType == MessageType.Document))
                    {
                        try
                        {
                            mediaData = await File.ReadAllBytesAsync(mediaFilePath);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: Could not read media data into memory: {ex.Message}");
                            // Continue without in-memory data - will use FilePath instead
                            mediaData = null;
                        }
                    }
                    mediaType = GetMimeType(fileInfo.Extension);
                    messageType = PendingMediaType;
                    
                    // If no text, set content to filename or media type indicator
                    if (string.IsNullOrWhiteSpace(messageText))
                    {
                        messageText = PendingMediaType switch
                        {
                            MessageType.Image => "📷 Image",
                            MessageType.Video => "🎥 Video",
                            MessageType.Audio => "🎵 Audio",
                            MessageType.Document => $"📄 {fileInfo.Name}",
                            _ => "📎 Attachment"
                        };
                    }
                    
                    // Clear pending media
                    PendingMediaPath = null;
                    PendingMediaType = MessageType.Text;
                    PendingMediaFileName = null;
                    PendingMediaFileSize = 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error processing media: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Diagnostics.Debug.WriteLine($"Error processing media: {ex.Message}");
                    return;
                }
            }

            MessageText = string.Empty; // Clear input immediately for better UX

            var message = new ConversationMessage
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = _selectedConversation.Id,
                Content = messageText,
                Direction = MessageDirection.Outgoing,
                Type = messageType,
                MediaData = mediaData,
                MediaType = mediaType,
                FilePath = mediaFilePath,
                Timestamp = DateTime.Now
            };

            try
            {
                // Add message to UI immediately - check for duplicates first
                if (!Messages.Any(m => m.Id == message.Id))
                {
                    Messages.Add(message);
                }
                
                // Send message (this will trigger AI response if it's an AI contact)
                await _communicationService.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending message: {ex.Message}");
                MessageBox.Show($"Error sending message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task SelectConversationAsync(Conversation conversation)
        {
            if (conversation == null)
                return;

            // If this is an AI contact, load the persona
            var contact = Contacts.FirstOrDefault(c => c.Id == conversation.ContactId);
            if (contact != null && contact.Type == ContactType.AI)
            {
                await LoadAIPersonaAsync(contact.Id);
            }

            SelectedConversation = conversation;
        }

        private async System.Threading.Tasks.Task StartConversationWithContactAsync(Contact contact)
        {
            if (contact == null)
                return;

            try
            {
                // Check if conversation already exists
                var existingConversation = Conversations.FirstOrDefault(c => c.Contact?.Id == contact.Id)?.Conversation;
                
                if (existingConversation != null)
                {
                    // Conversation exists, just select it
                    await SelectConversationAsync(existingConversation);
                }
                else
                {
                    // Create new conversation
                    var newConversation = new Conversation
                    {
                        Id = $"conv-{contact.Id}-{Guid.NewGuid()}",
                        ContactId = contact.Id,
                        LastMessageAt = DateTime.Now
                    };

                    // If this is an AI contact, load the persona
                    if (contact.Type == ContactType.AI)
                    {
                        await LoadAIPersonaAsync(contact.Id);
                    }

                    // Add conversation to the list
                    var convVm = new ConversationViewModel(newConversation, contact);
                    Conversations.Insert(0, convVm);

                    // Select the new conversation
                    SelectedConversation = newConversation;
                    ShowContactSelection = false;
                    ShowConversationList = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting conversation: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadAIPersonaAsync(string contactId)
        {
            try
            {
                var aiService = App.GetService<IAIService>();
                var persistenceService = App.GetService<IPersistenceService>();
                
                // Load AI contact from persistence
                var aiContact = await persistenceService.GetAsync<AIContact>($"AIContact_{contactId}");
                if (aiContact != null)
                {
                    // Load the model for this persona
                    await aiService.LoadModelAsync(aiContact);
                    aiContact.IsLoaded = true;
                    aiContact.LastUsedAt = DateTime.Now;
                    
                    // Save updated contact
                    await persistenceService.SetAsync($"AIContact_{aiContact.Id}", aiContact);
                    
                    System.Diagnostics.Debug.WriteLine($"Persona loaded: {aiContact.Name} with model {aiContact.ModelName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading AI persona: {ex.Message}");
            }
        }

        private string GetMimeType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".mp4" => "video/mp4",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".wmv" => "video/x-ms-wmv",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }

        private void CommunicationService_MessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            // Handle incoming messages on UI thread - check if message already exists to avoid duplicates
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Only add message if this conversation is currently selected
                if (_selectedConversation != null && e.ConversationId == _selectedConversation.Id)
                {
                    // Check if message already exists in the collection by ID - use direct comparison
                    // Re-check right before adding to prevent race conditions with LoadMessages
                    var messageExists = Messages.Any(m => m.Id == e.Message.Id);
                    
                    if (!messageExists)
                    {
                        Messages.Add(e.Message);
                        
                        // Re-sort by timestamp to maintain chronological order
                        // Use ID comparison instead of SequenceEqual which compares by reference
                        var sortedMessages = Messages.OrderBy(m => m.Timestamp).ToList();
                        bool needsResort = sortedMessages.Count != Messages.Count;
                        if (!needsResort)
                        {
                            // Check if messages are in the correct order by comparing IDs
                            for (int i = 0; i < Messages.Count; i++)
                            {
                                if (Messages[i].Id != sortedMessages[i].Id)
                                {
                                    needsResort = true;
                                    break;
                                }
                            }
                        }
                        
                        if (needsResort)
                        {
                            Messages.Clear();
                            foreach (var msg in sortedMessages)
                            {
                                Messages.Add(msg);
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Added incoming message {e.Message.Id} to UI. Total messages: {Messages.Count}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Message {e.Message.Id} already exists in UI collection. Skipping duplicate.");
                    }
                }
                
                // Update the conversation's last message in the list without reloading everything
                var conversationVm = Conversations.FirstOrDefault(c => c.Conversation.Id == e.ConversationId);
                if (conversationVm != null)
                {
                    // Update the conversation's last message timestamp
                    conversationVm.Conversation.LastMessageAt = e.Message.Timestamp;
                    
                    // Recreate the ConversationViewModel with updated last message using the message we already have
                    var index = Conversations.IndexOf(conversationVm);
                    if (index >= 0)
                    {
                        Conversations.RemoveAt(index);
                        var contact = Contacts.FirstOrDefault(c => c.Id == conversationVm.Conversation.ContactId);
                        Conversations.Insert(index, new ConversationViewModel(conversationVm.Conversation, contact, e.Message));
                        
                        // Sort conversations by last message time (move updated conversation to top)
                        var sorted = Conversations.OrderByDescending(c => c.LastMessageAt).ToList();
                        Conversations.Clear();
                        foreach (var conv in sorted)
                        {
                            Conversations.Add(conv);
                        }
                    }
                }
            });
        }

        private async Task StartCallAsync()
        {
            if (_selectedConversation == null)
                return;

            try
            {
                await _communicationService.StartVideoCallAsync(_selectedConversation.ContactId);
                var contact = _selectedContact ?? Contacts.FirstOrDefault(c => c.Id == _selectedConversation.ContactId);
                if (contact != null)
                {
                    OpenVideoCallWindow(contact, _selectedConversation.Id);
                }
                
                // Update call state
                OnPropertyChanged(nameof(IsCallActive));
                OnPropertyChanged(nameof(CanStartCall));
                OnPropertyChanged(nameof(CanEndCall));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting call: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error starting call: {ex.Message}");
            }
        }

        private async Task EndCallAsync()
        {
            if (_selectedConversation == null)
                return;

            try
            {
                await _communicationService.EndVideoCallAsync(_selectedConversation.Id);
                
                // Update call state
                CurrentCallState = CallState.None;
                OnPropertyChanged(nameof(IsCallActive));
                OnPropertyChanged(nameof(CanStartCall));
                OnPropertyChanged(nameof(CanEndCall));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error ending call: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error ending call: {ex.Message}");
            }
        }

        private async Task StartDialerCallAsync()
        {
            if (_selectedDialerContact == null)
                return;

            try
            {
                await _communicationService.StartVideoCallAsync(_selectedDialerContact.Id);
                OpenVideoCallWindow(_selectedDialerContact, null);
                
                // Update call state
                OnPropertyChanged(nameof(IsCallActive));
                OnPropertyChanged(nameof(CanStartDialerCall));
                OnPropertyChanged(nameof(CanEndDialerCall));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting call: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error starting dialer call: {ex.Message}");
            }
        }

        private async Task EndDialerCallAsync()
        {
            if (_selectedDialerContact == null)
                return;

            try
            {
                // Find conversation for this contact or create a temporary one
                var conversation = Conversations.FirstOrDefault(c => c.Contact?.Id == _selectedDialerContact.Id)?.Conversation;
                if (conversation != null)
                {
                    await _communicationService.EndVideoCallAsync(conversation.Id);
                }
                else
                {
                    // Create temporary conversation ID for ending call
                    var tempConversationId = $"conv-{_selectedDialerContact.Id}-temp";
                    await _communicationService.EndVideoCallAsync(tempConversationId);
                }
                
                // Update call state
                CurrentCallState = CallState.None;
                OnPropertyChanged(nameof(IsCallActive));
                OnPropertyChanged(nameof(CanStartDialerCall));
                OnPropertyChanged(nameof(CanEndDialerCall));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error ending call: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error ending dialer call: {ex.Message}");
            }
        }

        private void OpenVideoCallWindow(Contact contact, string? conversationId)
        {
            try
            {
                _eventAggregator.Publish(new ShowWindowEvent
                {
                    WindowType = "VideoCall",
                    Data = new VideoCallContext
                    {
                        ContactId = contact.Id,
                        ContactName = contact.Name,
                        ConversationId = conversationId
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening VideoCall window: {ex.Message}");
            }
        }

        private void CommunicationService_CallStateChanged(object? sender, CallStateChangedEventArgs e)
        {
            // Update call state if it's for the current conversation or dialer contact
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                bool isRelevantCall = false;
                
                // Check if it's for the selected conversation
                if (_selectedConversation != null && e.ConversationId == _selectedConversation.Id)
                {
                    isRelevantCall = true;
                }
                // Check if it's for the dialer contact (check if conversation ID contains the contact ID)
                else if (_showDialerView && _selectedDialerContact != null && 
                         (e.ConversationId.Contains(_selectedDialerContact.Id) || 
                          _selectedDialerContact.Id == e.ConversationId))
                {
                    isRelevantCall = true;
                }
                
                if (isRelevantCall)
                {
                    CurrentCallState = e.State;
                    OnPropertyChanged(nameof(IsCallActive));
                    OnPropertyChanged(nameof(CanStartCall));
                    OnPropertyChanged(nameof(CanEndCall));
                    OnPropertyChanged(nameof(CanStartDialerCall));
                    OnPropertyChanged(nameof(CanEndDialerCall));
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();

                    // If call is connected, convert new incoming messages to speech
                    if (e.State == CallState.Connected && _selectedConversation != null)
                    {
                        // Subscribe to messages during call for TTS
                        _communicationService.MessageReceived += HandleMessageDuringCall;
                    }
                    else if (e.State == CallState.Ended)
                    {
                        // Unsubscribe when call ends
                        _communicationService.MessageReceived -= HandleMessageDuringCall;
                    }
                }
            });
        }

        private async void HandleMessageDuringCall(object? sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (_selectedConversation == null || e.ConversationId != _selectedConversation.Id)
                    return;

                // Only speak incoming messages during active call
                if (e.Message.Direction == MessageDirection.Incoming && CurrentCallState == CallState.Connected)
                {
                    // Cast to communication service to access SpeakMessageAsync
                    if (_communicationService is HouseVictoria.Services.Communication.SMSMMSCommunicationService commService)
                    {
                        // Fire and forget - don't wait for TTS to complete
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await commService.SpeakMessageAsync(e.ConversationId, e.Message.Content);
                            }
                            catch (Exception ttsEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error speaking message during call: {ttsEx.Message}");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in HandleMessageDuringCall: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task ToggleAudioRecordingAsync()
        {
            if (_isRecordingAudio)
            {
                // Stop recording
                await StopAudioRecordingAsync();
            }
            else
            {
                // Start recording
                await StartAudioRecordingAsync();
            }
        }

        private async Task StartAudioRecordingAsync()
        {
            try
            {
                // Check if we have a selected conversation
                if (_selectedConversation == null)
                {
                    MessageBox.Show("Please select a conversation first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // For now, show a file picker to select an audio file
                // In a full implementation, this would use NAudio or similar to record from microphone
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Audio File to Transcribe",
                    Filter = "Audio Files|*.wav;*.mp3;*.m4a;*.ogg;*.flac|All Files|*.*"
                };

                if (dialog.ShowDialog() == true && File.Exists(dialog.FileName))
                {
                    IsRecordingAudio = true;
                    _recordedAudioPath = dialog.FileName;
                    _recordingDuration = TimeSpan.Zero;

                    // Transcribe the audio file
                    try
                    {
                        var audioData = await File.ReadAllBytesAsync(_recordedAudioPath);
                        
                        // Get AI service for transcription
                        var aiService = App.GetService<IAIService>();
                        if (aiService != null)
                        {
                            // Get the contact for this conversation
                            var contact = Contacts.FirstOrDefault(c => c.Id == _selectedConversation.ContactId);
                            if (contact != null && contact.Type == ContactType.AI)
                            {
                                // Use a temporary AI contact for transcription
                                var tempContact = new AIContact
                                {
                                    Id = contact.Id,
                                    Name = contact.Name,
                                    ServerEndpoint = contact.Type == ContactType.AI ? contact.AvatarUrl ?? "http://localhost:11434" : "http://localhost:11434"
                                };

                                var transcription = await aiService.ProcessAudioAsync(tempContact, audioData);
                                
                                // Set the transcribed text as the message
                                MessageText = transcription;
                                
                                MessageBox.Show($"Audio transcribed successfully!\n\nTranscription:\n{transcription}", 
                                    "Transcription Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                // For non-AI contacts, still try to transcribe (might use a default AI)
                                var defaultContact = new AIContact
                                {
                                    Id = "default",
                                    Name = "Default",
                                    ServerEndpoint = "http://localhost:11434"
                                };
                                
                                var transcription = await aiService.ProcessAudioAsync(defaultContact, audioData);
                                MessageText = transcription;
                                
                                MessageBox.Show($"Audio transcribed successfully!\n\nTranscription:\n{transcription}", 
                                    "Transcription Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show("AI service is not available for transcription.", "Service Unavailable", 
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error transcribing audio: {ex.Message}", "Transcription Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        System.Diagnostics.Debug.WriteLine($"Error transcribing audio: {ex.Message}\n{ex.StackTrace}");
                    }
                    finally
                    {
                        IsRecordingAudio = false;
                        _recordedAudioPath = null;
                        _recordingDuration = TimeSpan.Zero;
                    }
                }
                else
                {
                    // User cancelled - don't start recording
                    IsRecordingAudio = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting audio recording: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error starting audio recording: {ex.Message}\n{ex.StackTrace}");
                IsRecordingAudio = false;
            }
        }

        private async Task StopAudioRecordingAsync()
        {
            try
            {
                IsRecordingAudio = false;
                _recordingTimer?.Dispose();
                _recordingTimer = null;
                _recordingDuration = TimeSpan.Zero;
                
                // If we have a recorded file, transcribe it
                if (!string.IsNullOrWhiteSpace(_recordedAudioPath) && File.Exists(_recordedAudioPath))
                {
                    // Transcription is handled in StartAudioRecordingAsync after file selection
                    // This method is called when stopping, but the actual work is done in Start
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping audio recording: {ex.Message}");
            }
        }
    }

    public class ConversationViewModel : ObservableObject
    {
        public Conversation Conversation { get; }
        public Contact? Contact { get; }
        private readonly ConversationMessage? _lastMessage;
        
        public string DisplayName => Contact?.Name ?? Conversation.ContactId;
        public string LastMessagePreview => _lastMessage != null 
            ? (_lastMessage.Content.Length > 50 ? _lastMessage.Content.Substring(0, 50) + "..." : _lastMessage.Content)
            : "Tap to start conversation";
        public DateTime LastMessageAt => Conversation.LastMessageAt;

        public ConversationViewModel(Conversation conversation, Contact? contact, ConversationMessage? lastMessage = null)
        {
            Conversation = conversation;
            Contact = contact;
            _lastMessage = lastMessage;
        }
    }
}
