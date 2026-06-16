using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Persistence;
using HouseVictoria.Services.Persona;

namespace HouseVictoria.Services.Communication
{
    /// <summary>
    /// Service for SMS/MMS communication
    /// </summary>
    public class SMSMMSCommunicationService : ICommunicationService
    {
        private readonly List<Contact> _contacts = new();
        private readonly List<Conversation> _conversations = new();
        private readonly Dictionary<string, List<ConversationMessage>> _messages = new();
        private readonly Dictionary<string, AIContact> _aiContacts = new(); // Map Contact ID to AIContact
        private readonly Dictionary<string, List<ChatMessage>> _chatContexts = new(); // Store chat context for AI conversations
        private readonly IAIService? _aiService;
        private readonly IPersistenceService? _persistenceService;
        private readonly IMemoryService? _memoryService;
        private readonly IFileGenerationService? _fileGenerationService;
        private readonly IVoiceCallEngineService? _voiceEngine;
        private readonly AppConfig? _appConfig;
        private readonly PersonaChatContextBuilder _personaContextBuilder;
        /// <summary>Serialize image jobs — A2E/ComfyUI fail when many run at once.</summary>
        private readonly SemaphoreSlim _imageGenerationLock = new(1, 1);
        private readonly Dictionary<string, string> _lastImagePromptByConversation = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CallState> _activeCalls = new(); // Track active calls by conversation ID
        private readonly HashSet<string> _pendingFollowUps = new(); // Conversations with a scheduled auto follow-up

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<CallStateChangedEventArgs>? CallStateChanged;

        public SMSMMSCommunicationService(IAIService? aiService = null, IPersistenceService? persistenceService = null, IMemoryService? memoryService = null, IFileGenerationService? fileGenerationService = null, IJournalService? journalService = null, IVoiceCallEngineService? voiceEngine = null, AppConfig? appConfig = null)
        {
            _aiService = aiService;
            _persistenceService = persistenceService;
            _memoryService = memoryService;
            _fileGenerationService = fileGenerationService;
            _voiceEngine = voiceEngine;
            _appConfig = appConfig;
            _personaContextBuilder = new PersonaChatContextBuilder(memoryService, journalService);

            // Subscribe to AI service events if available
            if (_aiService != null)
            {
                _aiService.MessageReceived += AIService_MessageReceived;
                _aiService.ErrorOccurred += AIService_ErrorOccurred;
            }

            // Initialize data asynchronously
            _ = InitializeDataAsync();
        }

        private async Task InitializeDataAsync()
        {
            // Load AI contacts from persistence if available
            if (_persistenceService != null)
            {
                try
                {
                    var savedContacts = await _persistenceService.GetAllAsync<AIContact>();
                    foreach (var aiContact in savedContacts.Values)
                    {
                        // Convert AIContact to Contact for display
                        var contact = new Contact
                        {
                            Id = aiContact.Id,
                            Name = aiContact.Name,
                            PhoneNumber = null,
                            Type = ContactType.AI,
                            AvatarUrl = aiContact.AvatarUrl
                        };
                        _contacts.Add(contact);
                        _aiContacts[aiContact.Id] = aiContact;

                        // Create conversation for this AI contact
                        var conversation = _conversations.FirstOrDefault(c => c.ContactId == aiContact.Id);
                        if (conversation == null)
                        {
                            conversation = new Conversation
                            {
                                Id = $"conv-{aiContact.Id}",
                                ContactId = aiContact.Id,
                                LastMessageAt = aiContact.LastUsedAt
                            };
                            _conversations.Add(conversation);
                            _chatContexts[conversation.Id] = new List<ChatMessage>();

                            // Load existing messages from persistence for this conversation
                            if (_persistenceService is DatabasePersistenceService dbServiceInit)
                            {
                                try
                                {
                                    var existingMessages = await dbServiceInit.GetMessagesAsync(conversation.Id, 100);
                                    if (existingMessages.Count > 0)
                                    {
                                        _messages[conversation.Id] = existingMessages;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error loading messages for conversation {conversation.Id}: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            // Load existing messages from persistence for existing conversation
                            if (_persistenceService is DatabasePersistenceService dbServiceInit2 && !_messages.ContainsKey(conversation.Id))
                            {
                                try
                                {
                                    var existingMessages = await dbServiceInit2.GetMessagesAsync(conversation.Id, 100);
                                    if (existingMessages.Count > 0)
                                    {
                                        _messages[conversation.Id] = existingMessages;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error loading messages for conversation {conversation.Id}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading AI contacts from persistence: {ex.Message}");
                }
            }

            // Create AI contacts if AI service is available (fallback to sample data)
            if (_aiService != null && _aiContacts.Count == 0)
            {
                var aiContact1 = new AIContact
                {
                    Id = "ai-1",
                    Name = "AI Assistant",
                    ModelName = "llama3.2",
                    ServerEndpoint = "http://localhost:11434",
                    SystemPrompt = "You are a helpful AI assistant. Be friendly, concise, and helpful.",
                    Description = "General purpose AI assistant",
                    IsPrimaryAI = true
                };

                var aiContact2 = new AIContact
                {
                    Id = "ai-2",
                    Name = "Code Helper",
                    ModelName = "codellama",
                    ServerEndpoint = "http://localhost:11434",
                    SystemPrompt = "You are a coding assistant. Help with programming questions, code review, and debugging.",
                    Description = "Specialized coding assistant"
                };

                // Convert AIContacts to Contacts for display
                var contact2 = new Contact
                {
                    Id = aiContact1.Id,
                    Name = aiContact1.Name,
                    PhoneNumber = null,
                    Type = ContactType.AI,
                    AvatarUrl = aiContact1.AvatarUrl
                };

                var contact3 = new Contact
                {
                    Id = aiContact2.Id,
                    Name = aiContact2.Name,
                    PhoneNumber = null,
                    Type = ContactType.AI,
                    AvatarUrl = aiContact2.AvatarUrl
                };

                _contacts.Add(contact2);
                _contacts.Add(contact3);
                _aiContacts[aiContact1.Id] = aiContact1;
                _aiContacts[aiContact2.Id] = aiContact2;

                // Create conversations for AI contacts
                var conv2 = new Conversation
                {
                    Id = "conv-ai-1",
                    ContactId = aiContact1.Id,
                    LastMessageAt = DateTime.Now.AddHours(-2)
                };

                var conv3 = new Conversation
                {
                    Id = "conv-ai-2",
                    ContactId = aiContact2.Id,
                    LastMessageAt = DateTime.Now.AddDays(-1)
                };

                _conversations.Add(conv2);
                _conversations.Add(conv3);

                // Initialize chat contexts
                _chatContexts[conv2.Id] = new List<ChatMessage>();
                _chatContexts[conv3.Id] = new List<ChatMessage>();

                // Add welcome message
                _messages[conv2.Id] = new List<ConversationMessage>
                {
                    new ConversationMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        ConversationId = conv2.Id,
                        Content = "Hello! I'm your AI assistant. How can I help you today?",
                        Direction = MessageDirection.Incoming,
                        Timestamp = DateTime.Now.AddHours(-2)
                    }
                };
            }
        }

        private async void AIService_MessageReceived(object? sender, AIMessageEventArgs e)
        {
            // This handler receives intermediate messages from the AI service (including thinking tokens).
            // We do NOT display these to the UI - only the final processed response from SendMessageAsync
            // should be shown to the user. This prevents showing the AI's thinking process.

            // Still save to memory/context for internal processing, but don't update UI
            if (_aiContacts.TryGetValue(e.ContactId, out var aiContact) && _memoryService != null)
            {
                try
                {
                    // Find conversation for this AI contact
                    var conversation = _conversations.FirstOrDefault(c => c.ContactId == e.ContactId);
                    if (conversation != null)
                    {
                        // Get the last user message from chat context if available
                        string lastUserMessage = "";
                        if (_chatContexts.TryGetValue(conversation.Id, out var chatContext) && chatContext != null)
                        {
                            var lastUserMsg = chatContext.LastOrDefault(m => m != null && m.Role == "user");
                            lastUserMessage = lastUserMsg?.Content ?? "";
                        }

                        // Save the conversation exchange as a memory (for internal use only)
                        var experience = $"User: {lastUserMessage}\nAI: {e.Message}\nTimestamp: {e.Timestamp:yyyy-MM-dd HH:mm:ss}";
                        await _memoryService.AddMemoryAsync(e.ContactId, experience);

                        // Also save to data bank if available
                        var dataBanks = await _memoryService.GetAllDataBanksAsync();
                        if (dataBanks != null && !string.IsNullOrWhiteSpace(aiContact?.Name))
                        {
                            var personaDataBank = dataBanks.FirstOrDefault(db => db != null && !string.IsNullOrWhiteSpace(db.Name) && db.Name.Contains(aiContact.Name));
                            if (personaDataBank != null && !string.IsNullOrWhiteSpace(personaDataBank.Id))
                            {
                                await _memoryService.AddDataToBankAsync(personaDataBank.Id, experience);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving AI experience: {ex.Message}\n{ex.StackTrace}");
                }
            }

            // Do NOT fire MessageReceived event here - only the final processed response should be displayed
        }

        private void AIService_ErrorOccurred(object? sender, AIEErrorEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"AI Service Error: {e.ErrorMessage}");
            // Could show error message to user or log it
        }

        public Task<List<Contact>> GetContactsAsync()
        {
            return Task.FromResult(_contacts);
        }

        public Task<List<Conversation>> GetConversationsAsync()
        {
            // Ensure one conversation per contact - deduplicate by ContactId, keep most recent
            var deduplicated = _conversations
                .GroupBy(c => c.ContactId)
                .Select(g => g.OrderByDescending(c => c.LastMessageAt).First())
                .OrderByDescending(c => c.LastMessageAt)
                .ToList();
            return Task.FromResult(deduplicated);
        }

        public async Task RefreshContactsAsync()
        {
            _contacts.Clear();
            _conversations.Clear();
            _aiContacts.Clear();

            if (_persistenceService != null)
            {
                try
                {
                    var savedContacts = await _persistenceService.GetAllAsync<AIContact>();
                    foreach (var aiContact in savedContacts.Values)
                    {
                        var contact = new Contact
                        {
                            Id = aiContact.Id,
                            Name = aiContact.Name,
                            PhoneNumber = null,
                            Type = ContactType.AI,
                            AvatarUrl = aiContact.AvatarUrl
                        };
                        _contacts.Add(contact);
                        _aiContacts[aiContact.Id] = aiContact;

                        var conversation = new Conversation
                        {
                            Id = $"conv-{aiContact.Id}",
                            ContactId = aiContact.Id,
                            LastMessageAt = aiContact.LastUsedAt
                        };
                        _conversations.Add(conversation);
                        if (!_chatContexts.ContainsKey(conversation.Id))
                            _chatContexts[conversation.Id] = new List<ChatMessage>();

                        if (_persistenceService is DatabasePersistenceService dbService)
                        {
                            try
                            {
                                var existingMessages = await dbService.GetMessagesAsync(conversation.Id, 100);
                                if (existingMessages.Count > 0)
                                    _messages[conversation.Id] = existingMessages;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error loading messages for conversation {conversation.Id}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error refreshing AI contacts from persistence: {ex.Message}");
                }
            }

            // Fallback to sample data if no persisted contacts (first-time setup)
            if (_aiService != null && _aiContacts.Count == 0)
            {
                var aiContact1 = new AIContact
                {
                    Id = "ai-1",
                    Name = "AI Assistant",
                    ModelName = "llama3.2",
                    ServerEndpoint = "http://localhost:11434",
                    SystemPrompt = "You are a helpful AI assistant. Be friendly, concise, and helpful.",
                    Description = "General purpose AI assistant",
                    IsPrimaryAI = true
                };
                var aiContact2 = new AIContact
                {
                    Id = "ai-2",
                    Name = "Code Helper",
                    ModelName = "codellama",
                    ServerEndpoint = "http://localhost:11434",
                    SystemPrompt = "You are a coding assistant. Help with programming questions, code review, and debugging.",
                    Description = "Specialized coding assistant"
                };
                foreach (var ac in new[] { aiContact1, aiContact2 })
                {
                    _contacts.Add(new Contact { Id = ac.Id, Name = ac.Name, PhoneNumber = null, Type = ContactType.AI, AvatarUrl = ac.AvatarUrl });
                    _aiContacts[ac.Id] = ac;
                    var conv = new Conversation { Id = $"conv-{ac.Id}", ContactId = ac.Id, LastMessageAt = DateTime.Now };
                    _conversations.Add(conv);
                    _chatContexts[conv.Id] = new List<ChatMessage>();
                }
            }
        }

        public Task<Conversation> GetOrCreateConversationForContactAsync(string contactId)
        {
            var existing = _conversations.FirstOrDefault(c => c.ContactId == contactId);
            if (existing != null)
                return Task.FromResult(existing);

            var conversation = new Conversation
            {
                Id = $"conv-{contactId}",
                ContactId = contactId,
                LastMessageAt = DateTime.Now
            };
            _conversations.Add(conversation);
            if (!_chatContexts.ContainsKey(conversation.Id))
                _chatContexts[conversation.Id] = new List<ChatMessage>();
            return Task.FromResult(conversation);
        }

        public async Task<List<ConversationMessage>> GetMessagesAsync(string conversationId)
        {
            // First check in-memory cache
            if (_messages.TryGetValue(conversationId, out var cachedMessages) && cachedMessages.Count > 0)
            {
                // Return last 100 messages from cache, deduplicated by ID
                return cachedMessages
                    .GroupBy(m => m.Id)
                    .Select(g => g.First())
                    .OrderBy(m => m.Timestamp)
                    .TakeLast(100)
                    .ToList();
            }

            // Load from persistence if available
            if (_persistenceService != null)
            {
                try
                {
                    var persistedMessages = await ((DatabasePersistenceService)_persistenceService).GetMessagesAsync(conversationId, 100).ConfigureAwait(false);

                    // Deduplicate persisted messages by ID
                    var deduplicatedPersisted = persistedMessages
                        .GroupBy(m => m.Id)
                        .Select(g => g.First())
                        .ToList();

                    // Update in-memory cache with deduplicated messages
                    if (deduplicatedPersisted.Count > 0)
                    {
                        // Merge with existing cache instead of replacing to preserve in-memory messages
                        if (_messages.TryGetValue(conversationId, out var existingMessages))
                        {
                            var existingIds = new HashSet<string>(existingMessages.Select(m => m.Id));
                            var newMessages = deduplicatedPersisted.Where(m => !existingIds.Contains(m.Id)).ToList();
                            existingMessages.AddRange(newMessages);
                            _messages[conversationId] = existingMessages
                                .GroupBy(m => m.Id)
                                .Select(g => g.First())
                                .OrderBy(m => m.Timestamp)
                                .ToList();
                        }
                        else
                        {
                            _messages[conversationId] = deduplicatedPersisted;
                        }

                        return _messages[conversationId].OrderBy(m => m.Timestamp).TakeLast(100).ToList();
                    }

                    return deduplicatedPersisted;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading messages from persistence: {ex.Message}");
                }
            }

            // Fallback to empty list
            return new List<ConversationMessage>();
        }

        public async Task<ConversationMessage?> GetLastMessageAsync(string conversationId)
        {
            // Check cache first
            if (_messages.TryGetValue(conversationId, out var cachedMessages) && cachedMessages.Count > 0)
            {
                return cachedMessages.OrderByDescending(m => m.Timestamp).FirstOrDefault();
            }

            // Load from persistence if available
            if (_persistenceService is DatabasePersistenceService dbService)
            {
                try
                {
                    return await dbService.GetLastMessageAsync(conversationId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading last message from persistence: {ex.Message}");
                }
            }

            return null;
        }

        public async Task SendMessageAsync(ConversationMessage message)
        {
            // If conversation doesn't exist, try to find it or create it
            var conversation = _conversations.FirstOrDefault(c => c.Id == message.ConversationId);
            if (conversation == null)
            {
                var contactId = ConversationContactResolver.ExtractContactId(
                    message.ConversationId,
                    _aiContacts.Keys);

                conversation = new Conversation
                {
                    Id = message.ConversationId,
                    ContactId = contactId,
                    LastMessageAt = DateTime.Now
                };
                _conversations.Add(conversation);

                // Initialize chat context for this conversation
                if (!_chatContexts.ContainsKey(message.ConversationId))
                {
                    _chatContexts[message.ConversationId] = new List<ChatMessage>();
                }
            }

            if (!_messages.ContainsKey(message.ConversationId))
            {
                _messages[message.ConversationId] = new List<ConversationMessage>();
            }

            // Ensure chat context exists for this conversation
            if (!_chatContexts.ContainsKey(message.ConversationId))
            {
                _chatContexts[message.ConversationId] = new List<ChatMessage>();
            }
            message.Direction = MessageDirection.Outgoing;
            message.Timestamp = DateTime.Now;

            // Check for duplicates before adding to cache
            if (!_messages[message.ConversationId].Any(m => m.Id == message.Id))
            {
                _messages[message.ConversationId].Add(message);
            }

            // Persist message to database
            if (_persistenceService is DatabasePersistenceService dbService)
            {
                try
                {
                    await dbService.SaveMessageAsync(message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving message to persistence: {ex.Message}");
                }
            }

            // Update conversation timestamp
            conversation.LastMessageAt = message.Timestamp;

            // If this is a message to an AI contact, route through AI service
            if (conversation != null && _aiService != null)
            {
                var contact = _contacts.FirstOrDefault(c => c.Id == conversation.ContactId);
                if (contact != null && contact.Type == ContactType.AI)
                {
                    var aiContact = await ResolveAiContactForChatAsync(contact.Id).ConfigureAwait(false);
                    if (aiContact == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"No AIContact resolved for chat contact id={contact.Id} name={contact.Name}");
                    }
                    else
                    {
                    System.Diagnostics.Debug.WriteLine(
                        $"Chat routing: persona={aiContact.Name} id={aiContact.Id} primary={aiContact.IsPrimaryAI} role={aiContact.Role}");
                    try
                    {
                        // Get chat context for this conversation
                        if (!_chatContexts.ContainsKey(message.ConversationId))
                        {
                            _chatContexts[message.ConversationId] = new List<ChatMessage>();
                        }

                        var context = _chatContexts[message.ConversationId];

                        // Add user message to context
                        context.Add(new ChatMessage
                        {
                            Role = "user",
                            Content = message.Content,
                            Timestamp = message.Timestamp
                        });

                        // If user asked for image generation, generate and attach to chat + File Retrieval
                        if (ShouldGenerateImageForMessage(message.ConversationId, message.Content))
                        {
                            var queued = _imageGenerationLock.CurrentCount == 0;
                            await PublishIncomingTextAsync(
                                conversation,
                                message.ConversationId,
                                queued
                                    ? "🎨 Queued — finishing your previous image, then starting this one…"
                                    : "🎨 Generating your image… this can take up to a minute.");

                            await _imageGenerationLock.WaitAsync().ConfigureAwait(false);
                            ImageGenerationResult imageResult;
                            try
                            {
                                imageResult = await ProcessImageGenerationRequestAsync(aiContact, message.Content, message.ConversationId);
                            }
                            finally
                            {
                                _imageGenerationLock.Release();
                            }

                            if (imageResult.Success && imageResult.ImageBytes != null && !string.IsNullOrWhiteSpace(imageResult.ChatFilePath))
                            {
                                context.Add(new ChatMessage { Role = "assistant", Content = imageResult.Message, Timestamp = DateTime.Now, ModelUsed = aiContact.ModelName });

                                var imageBytes = imageResult.ImageBytes;
                                var imageResponseMsg = new ConversationMessage
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ConversationId = message.ConversationId,
                                    Content = imageResult.Message,
                                    Direction = MessageDirection.Incoming,
                                    Type = MessageType.Image,
                                    FilePath = imageResult.ChatFilePath,
                                    MediaType = "image/png",
                                    MediaData = imageBytes.Length <= 10 * 1024 * 1024 ? imageBytes : null,
                                    Timestamp = DateTime.Now
                                };

                                if (!_messages.ContainsKey(message.ConversationId))
                                    _messages[message.ConversationId] = new List<ConversationMessage>();
                                if (!_messages[message.ConversationId].Any(m => m.Id == imageResponseMsg.Id))
                                    _messages[message.ConversationId].Add(imageResponseMsg);
                                if (_persistenceService is DatabasePersistenceService dbImg)
                                {
                                    try { await dbImg.SaveMessageAsync(imageResponseMsg); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error saving image response message: {ex.Message}"); }
                                }
                                conversation.LastMessageAt = imageResponseMsg.Timestamp;
                                MessageReceived?.Invoke(this, new MessageReceivedEventArgs { Message = imageResponseMsg, ConversationId = message.ConversationId });
                                if (context.Count > 20) context.RemoveRange(0, context.Count - 20);
                                return;
                            }

                            // imageSuccess false = error; imageResult.Message contains error text
                            context.Add(new ChatMessage { Role = "assistant", Content = imageResult.Message, Timestamp = DateTime.Now, ModelUsed = aiContact.ModelName });
                            var errMsg = new ConversationMessage
                            {
                                Id = Guid.NewGuid().ToString(),
                                ConversationId = message.ConversationId,
                                Content = imageResult.Message,
                                Direction = MessageDirection.Incoming,
                                Type = MessageType.Text,
                                Timestamp = DateTime.Now
                            };
                            if (!_messages.ContainsKey(message.ConversationId))
                                _messages[message.ConversationId] = new List<ConversationMessage>();
                            _messages[message.ConversationId].Add(errMsg);
                            if (_persistenceService is DatabasePersistenceService dbErr)
                            {
                                try { await dbErr.SaveMessageAsync(errMsg); } catch { }
                            }
                            conversation.LastMessageAt = errMsg.Timestamp;
                            MessageReceived?.Invoke(this, new MessageReceivedEventArgs { Message = errMsg, ConversationId = message.ConversationId });
                            if (context.Count > 20) context.RemoveRange(0, context.Count - 20);
                            return;
                        }

                        // Retrieve relevant journals + memories and inject them so the AI is aware of
                        // its own prior work (research, strategies, projects) when replying.
                        if (_imageGenerationLock.CurrentCount == 0 && IsImageStatusInquiry(message.Content))
                        {
                            await PublishIncomingTextAsync(conversation, message.ConversationId, "⏳ Still generating your image — hang on, this can take up to a minute.");
                            if (context.Count > 20) context.RemoveRange(0, context.Count - 20);
                            return;
                        }

                        var retrieval = await BuildRetrievalContextAsync(aiContact, message.Content);
                        List<ChatMessage> contextForAi = context;
                        var imageGuard = BuildImageChatGuardNote(message.Content);
                        if (!string.IsNullOrWhiteSpace(retrieval) || !string.IsNullOrWhiteSpace(imageGuard))
                        {
                            var systemContent = string.Join("\n\n", new[] { retrieval, imageGuard }.Where(s => !string.IsNullOrWhiteSpace(s)));
                            contextForAi = new List<ChatMessage>(context.Count + 1)
                            {
                                new ChatMessage { Role = "system", Content = systemContent, Timestamp = DateTime.Now }
                            };
                            contextForAi.AddRange(context);
                        }

                        // Send to AI service
                        var aiResponse = await _aiService.SendMessageAsync(aiContact, message.Content, contextForAi);

                        // Check if user requested file creation or AI wants to create a file
                        var userRequestedFile = message.Content.Contains("file", StringComparison.OrdinalIgnoreCase) &&
                                               (message.Content.Contains("create", StringComparison.OrdinalIgnoreCase) ||
                                                message.Content.Contains("save", StringComparison.OrdinalIgnoreCase) ||
                                                message.Content.Contains("put", StringComparison.OrdinalIgnoreCase) ||
                                                message.Content.Contains("generate", StringComparison.OrdinalIgnoreCase));

                        // Process file creation if requested
                        var (fileCreated, responseMessage, fileName) = await ProcessFileCreationRequestAsync(
                            aiResponse,
                            aiContact.Id,
                            message.Content,
                            userRequestedFile);

                        // Add AI response to context (use the processed message)
                        context.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = responseMessage,
                            Timestamp = DateTime.Now,
                            ModelUsed = aiContact.ModelName
                        });

                        // Create and send the response message to the user
                        var responseMsg = new ConversationMessage
                        {
                            Id = Guid.NewGuid().ToString(),
                            ConversationId = message.ConversationId,
                            Content = responseMessage,
                            Direction = MessageDirection.Incoming,
                            Type = MessageType.Text,
                            Timestamp = DateTime.Now
                        };

                        if (!_messages.ContainsKey(message.ConversationId))
                        {
                            _messages[message.ConversationId] = new List<ConversationMessage>();
                        }
                        // Check for duplicates before adding to cache
                        if (!_messages[message.ConversationId].Any(m => m.Id == responseMsg.Id))
                        {
                            _messages[message.ConversationId].Add(responseMsg);
                        }

                        // Persist AI response message to database
                        if (_persistenceService is DatabasePersistenceService dbService2)
                        {
                            try
                            {
                                await dbService2.SaveMessageAsync(responseMsg);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error saving AI response message to persistence: {ex.Message}");
                            }
                        }

                        // Update conversation timestamp
                        conversation.LastMessageAt = responseMsg.Timestamp;

                        // Fire event to notify UI
                        MessageReceived?.Invoke(this, new MessageReceivedEventArgs
                        {
                            Message = responseMsg,
                            ConversationId = message.ConversationId
                        });

                        // Keep context size manageable (last 20 messages)
                        if (context.Count > 20)
                        {
                            context.RemoveRange(0, context.Count - 20);
                        }

                        // If she promised to "give a moment" / "be right back" / follow up,
                        // schedule a delayed follow-up so she actually delivers instead of going silent.
                        TryScheduleFollowUp(aiContact, message.ConversationId, responseMessage);
                    }
                    catch (TaskCanceledException ex)
                    {
                        // Handle timeout - HttpClient throws TaskCanceledException on timeout
                        System.Diagnostics.Debug.WriteLine($"AI Service Timeout: Request took too long. This may happen with:\n- Large context windows\n- High MaxTokens settings\n- Slow model responses\n- Network issues\nException: {ex.Message}");

                        var errorMessage = new ConversationMessage
                        {
                            Id = Guid.NewGuid().ToString(),
                            ConversationId = message.ConversationId,
                            Content = $"⏱️ Request Timeout: The AI response took too long to generate (timeout after 5 minutes).\n\nPossible causes:\n• Large context or high MaxTokens setting\n• Slow model performance\n• Network latency\n• Complex prompts requiring long processing\n\nTry:\n• Reducing MaxTokens in persona settings (currently: {aiContact.MaxTokens})\n• Reducing context length (currently: {aiContact.ContextLength})\n• Checking Ollama server status\n• Using a faster model\n• Simplifying your prompt",
                            Direction = MessageDirection.Incoming,
                            Type = MessageType.Text,
                            Timestamp = DateTime.Now
                        };

                        if (!_messages.ContainsKey(message.ConversationId))
                        {
                            _messages[message.ConversationId] = new List<ConversationMessage>();
                        }
                        _messages[message.ConversationId].Add(errorMessage);

                        // Fire event to notify UI
                        MessageReceived?.Invoke(this, new MessageReceivedEventArgs
                        {
                            Message = errorMessage
                        });
                    }
                    catch (Exception ex)
                    {
                        var errorDetails = ex is HttpRequestException httpEx
                            ? $"HTTP Error: {httpEx.Message}"
                            : $"Error: {ex.Message}";

                        System.Diagnostics.Debug.WriteLine($"Error sending message to AI: {errorDetails}");
                        if (ex.InnerException != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }

                        // Add error message to conversation
                        var errorMessage = new ConversationMessage
                        {
                            Id = Guid.NewGuid().ToString(),
                            ConversationId = message.ConversationId,
                            Content = $"❌ AI Service Error: {errorDetails}\n\nPlease check:\n1. Is Ollama running?\n2. Is the endpoint correct? ({aiContact.ServerEndpoint})\n3. Does the model '{aiContact.ModelName}' exist?\n4. Try reducing MaxTokens or context length if timeout occurred",
                            Direction = MessageDirection.Incoming,
                            Type = MessageType.Text,
                            Timestamp = DateTime.Now
                        };

                        if (!_messages.ContainsKey(message.ConversationId))
                        {
                            _messages[message.ConversationId] = new List<ConversationMessage>();
                        }
                        _messages[message.ConversationId].Add(errorMessage);

                        MessageReceived?.Invoke(this, new MessageReceivedEventArgs
                        {
                            Message = errorMessage,
                            ConversationId = message.ConversationId
                        });
                    }
                    }
                }
            }
        }

        /// <summary>Loads the latest persona record from persistence so chat uses current prompts and sharing flags.</summary>
        private async Task<AIContact?> ResolveAiContactForChatAsync(string contactId)
        {
            if (_persistenceService != null)
            {
                try
                {
                    var fresh = await _persistenceService.GetAsync<AIContact>($"AIContact_{contactId}").ConfigureAwait(false);
                    if (fresh != null)
                    {
                        _aiContacts[contactId] = fresh;
                        return fresh;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ResolveAiContactForChatAsync persistence: {ex.Message}");
                }
            }

            return _aiContacts.TryGetValue(contactId, out var cached) ? cached : null;
        }

        private async Task<(bool fileCreated, string responseMessage, string? fileName)> ProcessFileCreationRequestAsync(
            string aiResponse,
            string contactId,
            string userMessage,
            bool userRequestedFile)
        {
            if (_fileGenerationService == null)
            {
                return (false, aiResponse, null);
            }

            try
            {
                // Check if user requested file creation
                if (userRequestedFile)
                {
                    // Extract filename from user message or generate one
                    string fileName = ExtractFileNameFromMessage(userMessage) ??
                                     ExtractFileNameFromMessage(aiResponse) ??
                                     $"ai_generated_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                    // Extract content from AI response
                    // Look for markers like [FILE]...[/FILE] or code blocks, or use the entire response
                    string content = ExtractFileContent(aiResponse);

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        // If no markers found, use the entire response as content
                        // But remove any "copy/paste friendly" prefixes the AI might have added
                        content = aiResponse;

                        // Remove common prefixes like "Here's the content:", "Copy this:", etc.
                        var prefixes = new[] { "Here's the content:", "Copy this:", "Here it is:", "Here's your file:", "File content:" };
                        foreach (var prefix in prefixes)
                        {
                            if (content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                content = content.Substring(prefix.Length).Trim();
                                break;
                            }
                        }
                    }

                    // Create the file
                    var filePath = await _fileGenerationService.CreateTextFileAsync(fileName, content);

                    System.Diagnostics.Debug.WriteLine($"File created: {filePath}");

                    // Modify response to indicate file was created (remove the content, just show confirmation)
                    var modifiedResponse = aiResponse;

                    // If the response is just the content, replace it with a confirmation
                    if (content == aiResponse || content.Length > aiResponse.Length * 0.8)
                    {
                        modifiedResponse = $"✅ File created successfully!\n\n📄 Filename: {System.IO.Path.GetFileName(filePath)}\n📁 Location: File Retrieval\n\nYou can access it by clicking the File Retrieval button (📥) in the top tray.";
                    }
                    else
                    {
                        modifiedResponse = $"{aiResponse}\n\n✅ File created: {System.IO.Path.GetFileName(filePath)}\n📁 Location: File Retrieval";
                    }

                    return (true, modifiedResponse, System.IO.Path.GetFileName(filePath));
                }

                // Check for explicit file markers in AI response
                if (aiResponse.Contains("[FILE]", StringComparison.OrdinalIgnoreCase) ||
                    (aiResponse.Contains("```", StringComparison.OrdinalIgnoreCase) && userMessage.Contains("file", StringComparison.OrdinalIgnoreCase)))
                {
                    string fileName = ExtractFileNameFromMessage(userMessage) ??
                                     ExtractFileNameFromMessage(aiResponse) ??
                                     $"ai_generated_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    string content = ExtractFileContent(aiResponse);

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var filePath = await _fileGenerationService.CreateTextFileAsync(fileName, content);
                        System.Diagnostics.Debug.WriteLine($"File created from markers: {filePath}");

                        var modifiedResponse = aiResponse.Replace("[FILE]", "").Replace("[/FILE]", "");
                        modifiedResponse = $"{modifiedResponse}\n\n✅ File created: {System.IO.Path.GetFileName(filePath)}\n📁 Location: File Retrieval";

                        return (true, modifiedResponse, System.IO.Path.GetFileName(filePath));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing file creation request: {ex.Message}\n{ex.StackTrace}");
            }

            return (false, aiResponse, null);
        }

        /// <summary>
        /// Detects if the user is asking for image generation.
        /// </summary>
        private static bool IsImageGenerationRequest(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            var m = message.Trim();
            return m.Contains("draw", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("generate image", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("generate an image", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("generate a picture", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("create image", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("create an image", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("create a picture", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("make an image", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("make a picture", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("send me a picture", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("send me an image", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("send me a photo", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("send a picture", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("show me a picture", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("picture of", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("image of", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("photo of", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("stable diffusion", StringComparison.OrdinalIgnoreCase) ||
                   System.Text.RegularExpressions.Regex.IsMatch(m, @"(generate|create|make|send|show)\s+(?:me\s+)?(?:an?\s+)?(image|picture|photo)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Broader intent check so casual requests ("I want a picture of…") still trigger generation
        /// instead of a text-only reply where the persona claims to send an image.
        /// </summary>
        private bool ShouldGenerateImageForMessage(string conversationId, string message)
        {
            if (IsImageGenerationRequest(message))
                return true;
            if (IsImageFollowUpRequest(message) && _lastImagePromptByConversation.ContainsKey(conversationId))
                return true;
            if (string.IsNullOrWhiteSpace(message))
                return false;
            var m = message.Trim();
            var wantsVisual = m.Contains("picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("photo", StringComparison.OrdinalIgnoreCase)
                || m.Contains("image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("drawing", StringComparison.OrdinalIgnoreCase)
                || m.Contains("portrait", StringComparison.OrdinalIgnoreCase)
                || m.Contains("selfie", StringComparison.OrdinalIgnoreCase);
            if (!wantsVisual)
                return false;
            return System.Text.RegularExpressions.Regex.IsMatch(m,
                @"\b(send|show|give|make|create|generate|draw|paint|want|need|get|another|more|again)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private static bool IsImageFollowUpRequest(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;
            var m = message.Trim();
            if (m.Equals("again", StringComparison.OrdinalIgnoreCase)
                || m.Equals("retry", StringComparison.OrdinalIgnoreCase)
                || m.Equals("one more", StringComparison.OrdinalIgnoreCase)
                || m.Equals("another", StringComparison.OrdinalIgnoreCase)
                || m.Equals("more", StringComparison.OrdinalIgnoreCase))
                return true;
            return m.Contains("try again", StringComparison.OrdinalIgnoreCase)
                || m.Contains("another one", StringComparison.OrdinalIgnoreCase)
                || m.Contains("one more", StringComparison.OrdinalIgnoreCase)
                || m.Contains("do it again", StringComparison.OrdinalIgnoreCase)
                || m.Contains("send another", StringComparison.OrdinalIgnoreCase)
                || m.Contains("another picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("another image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("another photo", StringComparison.OrdinalIgnoreCase)
                || m.Contains("generate another", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveImagePrompt(string conversationId, string userMessage)
        {
            if (IsImageFollowUpRequest(userMessage)
                && _lastImagePromptByConversation.TryGetValue(conversationId, out var last)
                && !string.IsNullOrWhiteSpace(last))
            {
                return last;
            }
            return ExtractImagePrompt(userMessage);
        }

        private static bool IsImageStatusInquiry(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;
            var m = message.Trim();
            return m.Contains("where is", StringComparison.OrdinalIgnoreCase)
                || m.Contains("where's", StringComparison.OrdinalIgnoreCase)
                || m.Contains("wheres the", StringComparison.OrdinalIgnoreCase)
                || m.Contains("did you send", StringComparison.OrdinalIgnoreCase)
                || m.Contains("still working", StringComparison.OrdinalIgnoreCase)
                || m.Contains("is it ready", StringComparison.OrdinalIgnoreCase);
        }

        private static string? BuildImageChatGuardNote(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;
            var m = message.Trim();
            var mentionsImage = m.Contains("picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("photo", StringComparison.OrdinalIgnoreCase)
                || m.Contains("image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("selfie", StringComparison.OrdinalIgnoreCase);
            if (!mentionsImage)
                return null;
            return "[System: You cannot attach or transmit images in chat. Do not claim you sent, uploaded, or attached photos. Image delivery is handled separately by the app when the user explicitly requests generation.]";
        }

        private async Task PublishIncomingTextAsync(Conversation conversation, string conversationId, string content)
        {
            var msg = new ConversationMessage
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = conversationId,
                Content = content,
                Direction = MessageDirection.Incoming,
                Type = MessageType.Text,
                Timestamp = DateTime.Now
            };
            if (!_messages.ContainsKey(conversationId))
                _messages[conversationId] = new List<ConversationMessage>();
            if (!_messages[conversationId].Any(m => m.Id == msg.Id))
                _messages[conversationId].Add(msg);
            if (_persistenceService is DatabasePersistenceService db)
            {
                try { await db.SaveMessageAsync(msg).ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error saving status message: {ex.Message}"); }
            }
            conversation.LastMessageAt = msg.Timestamp;
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs { Message = msg, ConversationId = conversationId });
        }

        private sealed class ImageGenerationResult
        {
            public bool Success { get; init; }
            public string Message { get; init; } = string.Empty;
            public byte[]? ImageBytes { get; init; }
            public string? ChatFilePath { get; init; }
            public string? RetrievalFilePath { get; init; }
        }

        private static async Task<string> SaveImageToConversationMediaAsync(string conversationId, byte[] imageBytes, string extension = ".png")
        {
            var mediaDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Media", conversationId);
            Directory.CreateDirectory(mediaDir);
            var storedFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.GetFullPath(Path.Combine(mediaDir, storedFileName));
            await File.WriteAllBytesAsync(fullPath, imageBytes).ConfigureAwait(false);
            return fullPath;
        }

        /// <summary>
        /// Extracts the image prompt from the user message (e.g. "draw a cat" -> "a cat").
        /// </summary>
        private static string ExtractImagePrompt(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "a beautiful image";
            var m = message.Trim();
            var prefixes = new[]
            {
                "draw ", "draw a ", "draw an ",
                "generate image of ", "generate an image of ", "generate a picture of ", "generate image: ", "generate a picture: ",
                "create image of ", "create an image of ", "create a picture of ", "create image: ", "create a picture: ",
                "make an image of ", "make a picture of ",
                "send me a picture of ", "send me an image of ", "send me a photo of ",
                "send me a picture ", "send me an image ", "send me a photo ",
                "send a picture of ", "send an image of ",
                "show me a picture of ", "show me an image of ",
                "picture of ", "image of ", "photo of ",
                "generate ", "create ", "make ", "send me ", "show me "
            };
            foreach (var prefix in prefixes)
            {
                if (m.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var rest = m.Substring(prefix.Length).Trim();
                    if (!string.IsNullOrWhiteSpace(rest)) return rest;
                    break;
                }
            }
            return m;
        }

        /// <summary>
        /// Generates an image via A2E/ComfyUI, saves to File Retrieval and conversation media for inline chat display.
        /// </summary>
        private async Task<ImageGenerationResult> ProcessImageGenerationRequestAsync(AIContact aiContact, string userMessage, string conversationId)
        {
            if (_aiService == null)
                return new ImageGenerationResult { Success = false, Message = "❌ Image generation is not available. AI service is not configured." };
            try
            {
                var userPrompt = ResolveImagePrompt(conversationId, userMessage);
                var detailedPrompt = await _aiService.EnhanceImagePromptAsync(aiContact, userPrompt).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(detailedPrompt))
                    detailedPrompt = userPrompt;
                using var imageStream = await _aiService.GenerateImageAsync(aiContact, detailedPrompt).ConfigureAwait(false);
                using var ms = new MemoryStream();
                await imageStream.CopyToAsync(ms).ConfigureAwait(false);
                var imageBytes = ms.ToArray();
                if (imageBytes.Length == 0)
                    return new ImageGenerationResult { Success = false, Message = "❌ Image generation returned an empty file. Check Settings → Image Generation (A2E token or ComfyUI)." };

                _lastImagePromptByConversation[conversationId] = detailedPrompt;

                var chatFilePath = await SaveImageToConversationMediaAsync(conversationId, imageBytes);
                string? retrievalPath = null;
                if (_fileGenerationService != null)
                {
                    var fileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    retrievalPath = await _fileGenerationService.CreateFileAsync(fileName, imageBytes, null);
                }
                var caption = "Here's your image.";
                System.Diagnostics.Debug.WriteLine($"Image generated: chat={chatFilePath}, retrieval={retrievalPath} ({imageBytes.Length} bytes)");
                return new ImageGenerationResult
                {
                    Success = true,
                    Message = caption,
                    ImageBytes = imageBytes,
                    ChatFilePath = chatFilePath,
                    RetrievalFilePath = retrievalPath
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image generation failed: {ex.Message}\n{ex.StackTrace}");
                var msg = ex.Message;
                if (msg.Contains("coin", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("credit", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("balance", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("quota", StringComparison.OrdinalIgnoreCase))
                {
                    return new ImageGenerationResult
                    {
                        Success = false,
                        Message = $"❌ A2E credits may be exhausted: {msg}\n\nCheck your balance at video.a2e.ai or set Image Generation provider to ComfyUI in Settings."
                    };
                }
                var hint = "Start ComfyUI (e.g. http://localhost:8188) or check Settings → Image Generation.";
                var isConnectionRelated = ex is System.Net.Http.HttpRequestException
                    || msg.Contains("connection", StringComparison.OrdinalIgnoreCase) || msg.Contains("refused", StringComparison.OrdinalIgnoreCase) || msg.Contains("timed out", StringComparison.OrdinalIgnoreCase);
                if (isConnectionRelated)
                    return new ImageGenerationResult { Success = false, Message = $"❌ Image generation failed: {msg}\n\n{hint}" };
                return new ImageGenerationResult { Success = false, Message = $"❌ Image generation failed: {msg}" };
            }
        }

        private string? ExtractFileNameFromMessage(string message)
        {
            // Look for patterns like "create file.txt", "save as filename.txt", etc.
            var patterns = new[]
            {
                @"(?:create|save|generate|put).*?([a-zA-Z0-9_\-\.]+\.(txt|md|json|csv|xml|html|css|js|py|cs|cpp|h|hpp))",
                @"([a-zA-Z0-9_\-\.]+\.(txt|md|json|csv|xml|html|css|js|py|cs|cpp|h|hpp))",
                @"(?:file|filename|name).*?([a-zA-Z0-9_\-\.]+\.(txt|md|json|csv|xml|html|css|js|py|cs|cpp|h|hpp))"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(message, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        private string ExtractFileContent(string response)
        {
            // Look for [FILE]...[/FILE] markers
            var fileMarkerPattern = @"\[FILE\](.*?)\[/FILE\]";
            var match = System.Text.RegularExpressions.Regex.Match(response, fileMarkerPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }

            // Look for code blocks (```filename\ncontent\n```)
            var codeBlockPattern = @"```(?:[a-zA-Z0-9_\-\.]+)?\n(.*?)```";
            match = System.Text.RegularExpressions.Regex.Match(response, codeBlockPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }

            return string.Empty;
        }

        public Task SendMediaAsync(string conversationId, byte[] mediaData, string mediaType)
        {
            var message = new ConversationMessage
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = conversationId,
                Type = mediaType switch
                {
                    "image" => MessageType.Image,
                    "video" => MessageType.Video,
                    "audio" => MessageType.Audio,
                    _ => MessageType.Document
                },
                MediaData = mediaData,
                MediaType = mediaType,
                Direction = MessageDirection.Outgoing,
                Timestamp = DateTime.Now
            };

            return SendMessageAsync(message);
        }

        public async Task StartVideoCallAsync(string contactId)
        {
            try
            {
                // Find conversation for this contact
                var conversation = _conversations.FirstOrDefault(c => c.ContactId == contactId);
                if (conversation == null)
                {
                    // Create new conversation if it doesn't exist
                    conversation = new Conversation
                    {
                        Id = $"conv-{contactId}-{Guid.NewGuid()}",
                        ContactId = contactId,
                        LastMessageAt = DateTime.Now
                    };
                    _conversations.Add(conversation);
                }

                // Update call state
                conversation.CallState = CallState.Outgoing;
                _activeCalls[conversation.Id] = CallState.Outgoing;

                // Fire event
                CallStateChanged?.Invoke(this, new CallStateChangedEventArgs
                {
                    ConversationId = conversation.Id,
                    State = CallState.Outgoing,
                    Timestamp = DateTime.Now
                });

                // Simulate call connection after a short delay
                await Task.Delay(1000);

                conversation.CallState = CallState.Connected;
                _activeCalls[conversation.Id] = CallState.Connected;

                CallStateChanged?.Invoke(this, new CallStateChangedEventArgs
                {
                    ConversationId = conversation.Id,
                    State = CallState.Connected,
                    Timestamp = DateTime.Now
                });

                System.Diagnostics.Debug.WriteLine($"Call started for conversation {conversation.Id}");

                // Streaming voice engine owns mic + speakers (VAD -> STT -> LLM -> TTS on-device).
                var engineStarted = await TryStartVoiceEngineAsync(conversation.Id, contactId).ConfigureAwait(false);
                if (!engineStarted)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "VoiceEngine: failed to start. Check VoiceEngineEnabled in App.config and that the Python venv exists.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting call: {ex.Message}");
            }
        }

        /// <summary>
        /// Launches the external streaming speech-to-speech engine for this call, configured
        /// with the contact's persona (model, system prompt, voice). Returns true on success.
        /// </summary>
        private async Task<bool> TryStartVoiceEngineAsync(string conversationId, string contactId)
        {
            try
            {
                if (_voiceEngine == null || _appConfig == null || !_appConfig.VoiceEngineEnabled)
                    return false;

                var aiContact = await ResolveAiContactForChatAsync(contactId).ConfigureAwait(false);
                if (aiContact == null || string.IsNullOrWhiteSpace(aiContact.ModelName))
                {
                    System.Diagnostics.Debug.WriteLine("VoiceEngine: no usable AI contact/model; using legacy path.");
                    return false;
                }

                var withIdentity = HouseVictoria.Services.Persona.PersonaPromptComposer.WithIdentity(aiContact);
                const string voiceCallStyle =
                    "\n\nThis is a live voice call. Reply in one or two short, spoken sentences. " +
                    "Start your reply with a brief natural filler like \"Umm,\" \"So,\" or \"Well,\". " +
                    "Speak in the first person and sound conversational. " +
                    "Do not use markdown, bullet points, emojis, or stage directions.";
                var systemPrompt = (withIdentity.SystemPrompt ?? string.Empty) + voiceCallStyle;

                // Decide the LLM backend the same way text chat does (mirrors FallbackAIService):
                // a Hermes-primary house persona routes through Hermes; OpenAI-style endpoints
                // (Hermes / LM Studio / Anything LLM) use /v1/chat/completions; everything else
                // uses Ollama's native /api/chat.
                string backend, chatUrl, model;
                string? apiKey = null;

                if (ShouldUseHermesForVoice(aiContact))
                {
                    backend = "openai";
                    var hermes = string.IsNullOrWhiteSpace(_appConfig?.HermesEndpoint)
                        ? "http://127.0.0.1:8642/v1"
                        : _appConfig!.HermesEndpoint.Trim().TrimEnd('/');
                    if (!hermes.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                        hermes += "/v1";
                    chatUrl = hermes + "/chat/completions";
                    model = string.IsNullOrWhiteSpace(_appConfig?.HermesModelName) ? "hermes-agent" : _appConfig!.HermesModelName;
                    apiKey = _appConfig?.HermesApiKey;
                }
                else
                {
                    var baseEndpoint = string.IsNullOrWhiteSpace(aiContact.ServerEndpoint)
                        ? (string.IsNullOrWhiteSpace(_appConfig?.OllamaEndpoint) ? "http://localhost:11434" : _appConfig!.OllamaEndpoint)
                        : aiContact.ServerEndpoint;
                    baseEndpoint = baseEndpoint.Trim().TrimEnd('/');

                    if (IsOpenAiEndpoint(baseEndpoint))
                    {
                        backend = "openai";
                        chatUrl = baseEndpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
                            ? baseEndpoint
                            : baseEndpoint + "/chat/completions";
                    }
                    else
                    {
                        backend = "ollama";
                        chatUrl = baseEndpoint.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase)
                            ? baseEndpoint
                            : baseEndpoint + "/api/chat";
                    }
                    model = aiContact.ModelName;
                }

                var session = new VoiceCallEngineSession
                {
                    ConversationId = conversationId,
                    Model = model,
                    Backend = backend,
                    OllamaChatUrl = chatUrl,
                    ApiKey = apiKey,
                    SystemPrompt = systemPrompt,
                    Voice = ResolveEngineVoice(aiContact),
                    Speed = aiContact.AvatarVoiceSpeed > 0 ? aiContact.AvatarVoiceSpeed : 1.2,
                    Temperature = aiContact.Temperature > 0 ? aiContact.Temperature : 0.9
                };
                System.Diagnostics.Debug.WriteLine($"VoiceEngine: backend={backend}, url={chatUrl}, model={model}");

                var started = await _voiceEngine.StartAsync(session).ConfigureAwait(false);
                if (started)
                    System.Diagnostics.Debug.WriteLine($"VoiceEngine: active for conversation {conversationId}");
                return started;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TryStartVoiceEngineAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Picks a Kokoro voice for the engine. A persona's PiperVoiceId is normally a Piper
        /// model name (e.g. en_US-amy-medium), so only reuse it when it matches the Kokoro id
        /// pattern (e.g. af_nicole); otherwise fall back to the configured default.
        /// </summary>
        private string ResolveEngineVoice(AIContact aiContact)
        {
            var pv = aiContact.PiperVoiceId;
            if (!string.IsNullOrWhiteSpace(pv) &&
                System.Text.RegularExpressions.Regex.IsMatch(pv, "^[a-z]{2}_[a-z]+$"))
            {
                return pv;
            }
            return _appConfig?.VoiceEngineVoice ?? "af_nicole";
        }

        /// <summary>
        /// Mirrors FallbackAIService.ShouldUseHermes: a per-persona "hermes" flag wins, otherwise
        /// the primary house persona uses Hermes when Hermes is the configured primary LLM.
        /// </summary>
        private bool ShouldUseHermesForVoice(AIContact contact)
        {
            if (contact.AdditionalServers != null &&
                contact.AdditionalServers.TryGetValue("hermes", out var flag))
            {
                if (string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(flag, "false", StringComparison.OrdinalIgnoreCase)) return false;
            }

            var primary = (_appConfig?.PrimaryLLM ?? "ollama").Trim().ToLowerInvariant();
            if (primary == "hermes")
                return HouseVictoria.Services.Persona.PersonaPromptComposer.IsPrimaryPersona(contact);

            return false;
        }

        /// <summary>
        /// True when the endpoint speaks the OpenAI chat-completions protocol (LM Studio,
        /// Anything LLM, or any URL exposing a /v1 base).
        /// </summary>
        private bool IsOpenAiEndpoint(string? endpoint)
        {
            var e = (endpoint ?? string.Empty).TrimEnd('/');
            if (e.Length == 0) return false;
            var lm = (_appConfig?.LmStudioEndpoint ?? "http://localhost:1234/v1").TrimEnd('/');
            var allm = (_appConfig?.AnythingLLMEndpoint ?? "http://localhost:3001").TrimEnd('/');
            return e.Equals(lm, StringComparison.OrdinalIgnoreCase)
                || e.StartsWith("http://localhost:1234", StringComparison.OrdinalIgnoreCase)
                || e.Equals(allm, StringComparison.OrdinalIgnoreCase)
                || e.StartsWith("http://localhost:3001", StringComparison.OrdinalIgnoreCase)
                || e.EndsWith("/v1", StringComparison.OrdinalIgnoreCase);
        }

        public async Task EndVideoCallAsync(string conversationId)
        {
            try
            {
                if (_voiceEngine != null && _voiceEngine.IsRunning &&
                    string.Equals(_voiceEngine.ActiveConversationId, conversationId, StringComparison.Ordinal))
                {
                    await _voiceEngine.StopAsync().ConfigureAwait(false);
                }

                var conversation = _conversations.FirstOrDefault(c => c.Id == conversationId);
                if (conversation != null)
                {
                    conversation.CallState = CallState.Ended;
                    _activeCalls.Remove(conversationId);

                    CallStateChanged?.Invoke(this, new CallStateChangedEventArgs
                    {
                        ConversationId = conversationId,
                        State = CallState.Ended,
                        Timestamp = DateTime.Now
                    });

                    System.Diagnostics.Debug.WriteLine($"Call ended for conversation {conversationId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ending call: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        public Task ShareDocumentAsync(string conversationId, string filePath)
        {
            var message = new ConversationMessage
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = conversationId,
                Type = MessageType.Document,
                FilePath = filePath,
                Direction = MessageDirection.Outgoing,
                Timestamp = DateTime.Now
            };

            return SendMessageAsync(message);
        }

        public async Task DeleteConversationAsync(string conversationId)
        {
            try
            {
                _conversations.RemoveAll(c => c.Id == conversationId);
                _messages.Remove(conversationId);
                _chatContexts.Remove(conversationId);
                _activeCalls.Remove(conversationId);

                if (_persistenceService is DatabasePersistenceService dbService)
                {
                    await dbService.DeleteMessagesAsync(conversationId);
                }

                System.Diagnostics.Debug.WriteLine($"Deleted conversation {conversationId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting conversation: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteMessagesAsync(string conversationId, IReadOnlyList<string> messageIds)
        {
            if (messageIds == null || messageIds.Count == 0)
                return;

            try
            {
                if (_messages.TryGetValue(conversationId, out var msgList))
                {
                    var idsToRemove = new HashSet<string>(messageIds);
                    msgList.RemoveAll(m => idsToRemove.Contains(m.Id));
                }

                if (_persistenceService is DatabasePersistenceService dbService)
                {
                    foreach (var id in messageIds)
                    {
                        await dbService.DeleteMessageAsync(id);
                    }
                }

                if (_chatContexts.TryGetValue(conversationId, out var context))
                {
                    var idsToRemove = new HashSet<string>(messageIds);
                    var msgs = _messages.TryGetValue(conversationId, out var m) ? m : new List<ConversationMessage>();
                    var rebuilt = new List<ChatMessage>();
                    foreach (var msg in msgs.OrderBy(x => x.Timestamp))
                    {
                        if (idsToRemove.Contains(msg.Id)) continue;
                        rebuilt.Add(new ChatMessage
                        {
                            Role = msg.Direction == MessageDirection.Outgoing ? "user" : "assistant",
                            Content = msg.Content,
                            Timestamp = msg.Timestamp
                        });
                    }
                    _chatContexts[conversationId] = rebuilt;
                }

                var conv = _conversations.FirstOrDefault(c => c.Id == conversationId);
                if (conv != null && _messages.TryGetValue(conversationId, out var remaining) && remaining.Count > 0)
                {
                    conv.LastMessageAt = remaining.Max(m => m.Timestamp);
                }

                System.Diagnostics.Debug.WriteLine($"Deleted {messageIds.Count} messages from conversation {conversationId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting messages: {ex.Message}");
                throw;
            }
        }

        public async Task ArchiveConversationAsync(string conversationId)
        {
            if (_memoryService == null)
            {
                throw new InvalidOperationException("Memory service is not available for archiving.");
            }

            var conversation = _conversations.FirstOrDefault(c => c.Id == conversationId);
            if (conversation == null)
                throw new ArgumentException($"Conversation {conversationId} not found.");

            var contactId = conversation.ContactId;
            var messages = await GetMessagesAsync(conversationId);
            if (messages.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"No messages to archive for conversation {conversationId}");
                return;
            }

            var contact = _contacts.FirstOrDefault(c => c.Id == contactId);
            var contactName = contact?.Name ?? contactId;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Archived chat with {contactName} – {DateTime.Now:yyyy-MM-dd HH:mm}]");
            sb.AppendLine();
            foreach (var m in messages.OrderBy(x => x.Timestamp))
            {
                var role = m.Direction == MessageDirection.Outgoing ? "User" : contactName;
                var content = m.Type == MessageType.Text ? m.Content : $"[{m.Type}: {m.Content}]";
                sb.AppendLine($"{m.Timestamp:yyyy-MM-dd HH:mm} | {role}: {content}");
            }

            var archiveContent = sb.ToString();
            await _memoryService.AddMemoryAsync(contactId, archiveContent);

            System.Diagnostics.Debug.WriteLine($"Archived conversation {conversationId} ({messages.Count} messages) to AI long-term memory.");
        }

        #region Memory & journal retrieval (RAG-lite)

        private static string TruncateText(string? s, int max)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max).TrimEnd() + "…";
        }

        private Task<string?> BuildRetrievalContextAsync(AIContact contact, string userMessage)
        {
            var otherIds = _aiContacts.Keys.ToList();
            return _personaContextBuilder.BuildAsync(contact, userMessage, otherIds);
        }

        #endregion

        #region Conversational follow-up ("give me a moment" fulfilment)

        private static readonly string[] FollowUpCues =
        {
            "give me a moment", "give me a sec", "give me a second", "give me a minute", "give me some time",
            "one moment", "just a moment", "just a sec", "just a second", "hold on", "bear with me",
            "i'll be back", "i will be back", "be right back", "back in a", "brb",
            "let me think", "let me get back", "get back to you", "i'll get back",
            "i'll let you know", "i will let you know", "i'll find out", "i'll check", "let me check",
            "let me look", "let me research", "let me work on", "i'll work on", "working on it",
            "i'll put together", "i'll get that", "i'll come back", "let me dig", "let me pull"
        };

        private static bool DetectsFollowUpPromise(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var lower = text.ToLowerInvariant();
            return FollowUpCues.Any(cue => lower.Contains(cue));
        }

        private void TryScheduleFollowUp(AIContact aiContact, string conversationId, string promiseText)
        {
            if (_aiService == null || string.IsNullOrWhiteSpace(conversationId))
                return;
            if (!DetectsFollowUpPromise(promiseText))
                return;

            lock (_pendingFollowUps)
            {
                if (_pendingFollowUps.Contains(conversationId))
                    return;
                _pendingFollowUps.Add(conversationId);
            }

            var promiseTime = DateTime.Now;
            _ = Task.Run(async () =>
            {
                try
                {
                    await DeliverFollowUpAsync(aiContact, conversationId, promiseText, promiseTime).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Follow-up delivery failed: {ex.Message}");
                }
                finally
                {
                    lock (_pendingFollowUps)
                        _pendingFollowUps.Remove(conversationId);
                }
            });
        }

        private async Task DeliverFollowUpAsync(AIContact aiContact, string conversationId, string promiseText, DateTime promiseTime)
        {
            if (_aiService == null)
                return;

            // Wait a natural beat so the follow-up actually feels like "a moment later".
            await Task.Delay(TimeSpan.FromSeconds(45)).ConfigureAwait(false);

            // If the user already spoke again after the promise, they're engaged — don't barge in;
            // their new message will be answered through the normal path.
            if (_messages.TryGetValue(conversationId, out var existing) &&
                existing.Any(m => m.Direction == MessageDirection.Outgoing && m.Timestamp > promiseTime))
            {
                return;
            }

            var baseContext = _chatContexts.TryGetValue(conversationId, out var liveCtx)
                ? new List<ChatMessage>(liveCtx)
                : new List<ChatMessage>();

            var retrieval = await BuildRetrievalContextAsync(aiContact, promiseText).ConfigureAwait(false);
            var contextForAi = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(retrieval))
                contextForAi.Add(new ChatMessage { Role = "system", Content = retrieval, Timestamp = DateTime.Now });
            contextForAi.AddRange(baseContext);

            var followUpPrompt =
                "A moment ago you told the user you needed a moment, that you'd be right back, or that you'd follow up " +
                $"(you said something like: \"{TruncateText(promiseText, 200)}\"). " +
                "Time has now passed and you are back. Deliver on that promise directly and proactively: " +
                "present the answer, result, or next step you said you'd return with. " +
                "Do NOT ask the user to wait again, do NOT say you're still working on it, and do NOT dwell on apologising for the delay. " +
                "Continue naturally, as the next thing you say in the conversation.";

            var followUp = await _aiService.SendMessageAsync(aiContact, followUpPrompt, contextForAi).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(followUp))
                return;

            // Re-check after generation in case the user spoke while the model was thinking.
            if (_messages.TryGetValue(conversationId, out var existing2) &&
                existing2.Any(m => m.Direction == MessageDirection.Outgoing && m.Timestamp > promiseTime))
            {
                return;
            }

            if (_chatContexts.TryGetValue(conversationId, out var ctxToUpdate))
            {
                ctxToUpdate.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = followUp,
                    Timestamp = DateTime.Now,
                    ModelUsed = aiContact.ModelName
                });
                if (ctxToUpdate.Count > 20)
                    ctxToUpdate.RemoveRange(0, ctxToUpdate.Count - 20);
            }

            var followUpMsg = new ConversationMessage
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = conversationId,
                Content = followUp,
                Direction = MessageDirection.Incoming,
                Type = MessageType.Text,
                Timestamp = DateTime.Now
            };

            if (!_messages.ContainsKey(conversationId))
                _messages[conversationId] = new List<ConversationMessage>();
            if (!_messages[conversationId].Any(m => m.Id == followUpMsg.Id))
                _messages[conversationId].Add(followUpMsg);

            if (_persistenceService is DatabasePersistenceService dbFollow)
            {
                try { await dbFollow.SaveMessageAsync(followUpMsg).ConfigureAwait(false); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error saving follow-up message: {ex.Message}"); }
            }

            var conv = _conversations.FirstOrDefault(c => c.Id == conversationId);
            if (conv != null)
                conv.LastMessageAt = followUpMsg.Timestamp;

            MessageReceived?.Invoke(this, new MessageReceivedEventArgs
            {
                Message = followUpMsg,
                ConversationId = conversationId
            });

            System.Diagnostics.Debug.WriteLine($"Delivered auto follow-up for conversation {conversationId}");
        }

        #endregion
    }
}
