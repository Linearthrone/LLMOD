using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Persistence;
using NAudio.Wave;
using System.IO;

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
        private readonly ITTSService? _ttsService;
        private readonly Dictionary<string, CallState> _activeCalls = new(); // Track active calls by conversation ID

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<CallStateChangedEventArgs>? CallStateChanged;

        public SMSMMSCommunicationService(IAIService? aiService = null, IPersistenceService? persistenceService = null, IMemoryService? memoryService = null, IFileGenerationService? fileGenerationService = null, ITTSService? ttsService = null)
        {
            _aiService = aiService;
            _persistenceService = persistenceService;
            _memoryService = memoryService;
            _fileGenerationService = fileGenerationService;
            _ttsService = ttsService;
            
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
            // Initialize with some sample data
            var contact1 = new Contact
            {
                Id = "1",
                Name = "John Doe",
                PhoneNumber = "+1234567890",
                Type = ContactType.Human
            };
            
            _contacts.Add(contact1);
            
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
            
            // Create sample conversation for human contact
            var conv1 = new Conversation
            {
                Id = "conv1",
                ContactId = contact1.Id,
                LastMessageAt = DateTime.Now.AddMinutes(-30)
            };
            
            _conversations.Add(conv1);
            
            // Add sample messages
            _messages[conv1.Id] = new List<ConversationMessage>
            {
                new ConversationMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = conv1.Id,
                    Content = "Hey, how are you?",
                    Direction = MessageDirection.Incoming,
                    Timestamp = DateTime.Now.AddMinutes(-30)
                }
            };
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
            return Task.FromResult(_conversations);
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
                // Try to extract contact ID from conversation ID format: "conv-{contactId}-{guid}"
                var parts = message.ConversationId.Split('-');
                var contactId = parts.Length >= 2 ? parts[1] : message.ConversationId;
                
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
                if (contact != null && contact.Type == ContactType.AI && _aiContacts.TryGetValue(contact.Id, out var aiContact))
                {
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
                        
                        // If user asked for image generation (ComfyUI/Stable Diffusion), generate and save to File Retrieval (no link)
                        if (IsImageGenerationRequest(message.Content) && _fileGenerationService != null)
                        {
                            var (imageSuccess, imageResponseMessage) = await ProcessImageGenerationRequestAsync(aiContact, message.Content, message.ConversationId);
                            if (imageSuccess)
                            {
                                context.Add(new ChatMessage { Role = "assistant", Content = imageResponseMessage, Timestamp = DateTime.Now, ModelUsed = aiContact.ModelName });
                                var imageResponseMsg = new ConversationMessage
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ConversationId = message.ConversationId,
                                    Content = imageResponseMessage,
                                    Direction = MessageDirection.Incoming,
                                    Type = MessageType.Text,
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
                            // imageSuccess false = error; imageResponseMessage contains error text - fall through to show error as the reply
                            context.Add(new ChatMessage { Role = "assistant", Content = imageResponseMessage, Timestamp = DateTime.Now, ModelUsed = aiContact.ModelName });
                            var errMsg = new ConversationMessage
                            {
                                Id = Guid.NewGuid().ToString(),
                                ConversationId = message.ConversationId,
                                Content = imageResponseMessage,
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
                        
                        // Send to AI service
                        var aiResponse = await _aiService.SendMessageAsync(aiContact, message.Content, context);
                        
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
        /// Detects if the user is asking for image generation (ComfyUI/Stable Diffusion).
        /// </summary>
        private static bool IsImageGenerationRequest(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            var m = message.Trim();
            return m.Contains("draw", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("generate image", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("generate an image", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("create image", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("create an image", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("make an image", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("make a picture", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("picture of", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("image of", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("comfyui", StringComparison.OrdinalIgnoreCase) ||
                   m.Contains("stable diffusion", StringComparison.OrdinalIgnoreCase) ||
                   System.Text.RegularExpressions.Regex.IsMatch(m, @"(generate|create|make)\s+(?:an?\s+)?(image|picture|photo)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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
                "draw ", "draw a ", "draw an ", "generate image of ", "generate an image of ", "generate image: ",
                "create image of ", "create an image of ", "create image: ", "make an image of ", "make a picture of ",
                "picture of ", "image of ", "generate ", "create ", "make "
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
        /// Runs ComfyUI/Stable Diffusion image generation and saves the image to File Retrieval (no link).
        /// Uses the AI to turn the user's short request into a detailed, high-quality prompt first.
        /// Returns (true, successMessage) or (false, errorMessage).
        /// </summary>
        private async Task<(bool success, string responseMessage)> ProcessImageGenerationRequestAsync(AIContact aiContact, string userMessage, string conversationId)
        {
            if (_aiService == null || _fileGenerationService == null)
                return (false, "❌ Image generation is not available. File Retrieval service is not configured.");
            try
            {
                var userPrompt = ExtractImagePrompt(userMessage);
                // Have the AI expand the user's short request into a detailed, high-quality image prompt
                var detailedPrompt = await _aiService.EnhanceImagePromptAsync(aiContact, userPrompt);
                if (string.IsNullOrWhiteSpace(detailedPrompt))
                    detailedPrompt = userPrompt;
                using var imageStream = await _aiService.GenerateImageAsync(aiContact, detailedPrompt);
                using var ms = new MemoryStream();
                await imageStream.CopyToAsync(ms);
                var imageBytes = ms.ToArray();
                var fileName = $"comfyui_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filePath = await _fileGenerationService.CreateFileAsync(fileName, imageBytes, null);
                var responseMessage = $"✅ Image saved to File Retrieval.\n\n📄 Filename: {System.IO.Path.GetFileName(filePath)}\n📁 Location: File Retrieval\n\nOpen the File Retrieval button (📥) in the top tray to view it.";
                System.Diagnostics.Debug.WriteLine($"ComfyUI image saved to File Retrieval: {filePath}");
                return (true, responseMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ComfyUI image generation failed: {ex.Message}\n{ex.StackTrace}");
                var msg = ex.Message;
                var hint = "Start ComfyUI (e.g. http://localhost:8188) or check Settings → Stable Diffusion / ComfyUI.";
                var isComfyRelated = msg.Contains("ComfyUI", StringComparison.OrdinalIgnoreCase) || msg.Contains("8188", StringComparison.OrdinalIgnoreCase)
                    || ex is System.Net.Http.HttpRequestException
                    || msg.Contains("connection", StringComparison.OrdinalIgnoreCase) || msg.Contains("refused", StringComparison.OrdinalIgnoreCase) || msg.Contains("timed out", StringComparison.OrdinalIgnoreCase);
                if (isComfyRelated)
                    return (false, $"❌ Image generation failed: {msg}\n\n{hint}");
                return (false, $"❌ Image generation failed: {msg}");
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting call: {ex.Message}");
            }
        }

        public async Task EndVideoCallAsync(string conversationId)
        {
            try
            {
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

        /// <summary>
        /// Speaks a message using TTS during an active call
        /// </summary>
        public async Task SpeakMessageAsync(string conversationId, string text)
        {
            if (_ttsService == null)
            {
                System.Diagnostics.Debug.WriteLine("TTS: Service is not available (null)");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(text))
            {
                System.Diagnostics.Debug.WriteLine("TTS: Text is empty, cannot synthesize speech");
                return;
            }

            try
            {
                // Check if call is active
                if (!_activeCalls.TryGetValue(conversationId, out var callState) || callState != CallState.Connected)
                {
                    System.Diagnostics.Debug.WriteLine($"TTS: Call is not active for conversation {conversationId} (state: {callState})");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"TTS: Synthesizing speech for text: {text.Substring(0, Math.Min(50, text.Length))}...");

                // Use AI contact's Piper voice if this conversation is with an AI contact
                string? voice = null;
                var conversation = _conversations.FirstOrDefault(c => c.Id == conversationId);
                if (conversation != null && _aiContacts.TryGetValue(conversation.ContactId, out var aiContact) && !string.IsNullOrWhiteSpace(aiContact.PiperVoiceId))
                    voice = aiContact.PiperVoiceId;

                // Get audio data from TTS service
                var audioData = await _ttsService.SynthesizeSpeechAsync(text, voice);
                if (audioData == null || audioData.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("TTS: Service returned no audio data");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"TTS: Received {audioData.Length} bytes of audio data");

                // Save audio to temp file and play it
                var tempPath = Path.Combine(Path.GetTempPath(), $"tts_{Guid.NewGuid()}.wav");
                try
                {
                    await File.WriteAllBytesAsync(tempPath, audioData);
                    System.Diagnostics.Debug.WriteLine($"TTS: Saved audio to temp file: {tempPath}");

                    // Play audio using NAudio
                    await PlayAudioFileAsync(tempPath);

                    // Clean up temp file after a delay
                    _ = Task.Delay(5000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (File.Exists(tempPath))
                            {
                                File.Delete(tempPath);
                                System.Diagnostics.Debug.WriteLine($"TTS: Cleaned up temp file: {tempPath}");
                            }
                        }
                        catch (Exception deleteEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"TTS: Error deleting temp audio file: {deleteEx.Message}");
                        }
                    });
                }
                catch (Exception fileEx)
                {
                    System.Diagnostics.Debug.WriteLine($"TTS: Error writing or playing audio file: {fileEx.Message}\n{fileEx.StackTrace}");
                    // Clean up on error
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch (Exception cleanupEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"TTS: Error during cleanup: {cleanupEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS: Error speaking message: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task PlayAudioFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Audio file does not exist: {filePath}");
                    return;
                }

                // Use AudioFileReader which handles WAV, MP3, and other common formats
                WaveStream audioFile;
                try
                {
                    audioFile = new AudioFileReader(filePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create audio reader: {ex.Message}");
                    throw;
                }

                if (audioFile == null)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to create audio reader");
                    return;
                }

                using (audioFile)
                using (var outputDevice = new WaveOutEvent())
                {
                    try
                    {
                        outputDevice.Init(audioFile);
                        outputDevice.Play();

                        System.Diagnostics.Debug.WriteLine($"Playing audio file: {filePath}");

                        // Wait for playback to complete with timeout (max 30 seconds)
                        var timeout = DateTime.Now.AddSeconds(30);
                        while (outputDevice.PlaybackState == PlaybackState.Playing && DateTime.Now < timeout)
                        {
                            await Task.Delay(100);
                        }
                        
                        // Stop playback if still playing after timeout
                        if (outputDevice.PlaybackState == PlaybackState.Playing)
                        {
                            outputDevice.Stop();
                            System.Diagnostics.Debug.WriteLine("Audio playback timed out and was stopped");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Audio playback completed successfully");
                        }
                    }
                    finally
                    {
                        if (outputDevice.PlaybackState != PlaybackState.Stopped)
                        {
                            outputDevice.Stop();
                        }
                    }
                }
            }
            catch (NAudio.MmException ex)
            {
                // Handle audio device errors
                System.Diagnostics.Debug.WriteLine($"Audio device error: {ex.Message}");
            }
            catch (FormatException ex)
            {
                // Handle unsupported audio format
                System.Diagnostics.Debug.WriteLine($"Unsupported audio format: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing audio: {ex.Message}\n{ex.StackTrace}");
            }
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
    }
}
