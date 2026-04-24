using System.Text.Json;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Persistence;

namespace HouseVictoria.Services.RemoteCompanion
{
    /// <summary>
    /// Text (and transcribed voice) chat for the phone → PC remote API, using the same
    /// conversation id as the SMS window (<c>conv-{contactId}</c>) so history stays unified.
    /// </summary>
    public sealed class RemoteCompanionChatService
    {
        private readonly IAIService _aiService;
        private readonly DatabasePersistenceService _database;
        private readonly IMemoryService? _memoryService;
        private readonly IVirtualEnvironmentService? _virtualEnvironment;
        private readonly AppConfig _appConfig;

        public RemoteCompanionChatService(
            IAIService aiService,
            DatabasePersistenceService database,
            IMemoryService? memoryService,
            IVirtualEnvironmentService? virtualEnvironment,
            AppConfig appConfig)
        {
            _aiService = aiService;
            _database = database;
            _memoryService = memoryService;
            _virtualEnvironment = virtualEnvironment;
            _appConfig = appConfig;
        }

        public async Task<RemoteCompanionChatResult> ChatAsync(string userMessage, string? contactIdOverride, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return RemoteCompanionChatResult.Failure("message is required");

            var contact = await ResolveContactAsync(contactIdOverride).ConfigureAwait(false);
            if (contact == null)
                return RemoteCompanionChatResult.Failure("No AI contact found. Create one in the app or set RemoteCompanionAiContactId.");

            var conversationId = $"conv-{contact.Id}";
            var history = await _database.GetMessagesAsync(conversationId, 60).ConfigureAwait(false);

            var context = new List<ChatMessage>();
            foreach (var m in history.Where(x => x.Type == MessageType.Text))
            {
                var role = m.Direction == MessageDirection.Outgoing ? "user" : "assistant";
                context.Add(new ChatMessage { Role = role, Content = m.Content, Timestamp = m.Timestamp });
            }

            if (context.Count > 24)
                context = context.Skip(context.Count - 24).ToList();

            string reply;
            try
            {
                reply = await _aiService.SendMessageAsync(contact, userMessage.Trim(), context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return RemoteCompanionChatResult.Failure($"LLM error: {ex.Message}");
            }

            var userMsg = new ConversationMessage
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = conversationId,
                Content = userMessage.Trim(),
                Direction = MessageDirection.Outgoing,
                Type = MessageType.Text,
                Timestamp = DateTime.Now,
                IsRead = true
            };
            var assistantMsg = new ConversationMessage
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = conversationId,
                Content = reply,
                Direction = MessageDirection.Incoming,
                Type = MessageType.Text,
                Timestamp = DateTime.Now,
                IsRead = false
            };

            try
            {
                await _database.SaveMessageAsync(userMsg).ConfigureAwait(false);
                await _database.SaveMessageAsync(assistantMsg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoteCompanionChatService: save messages failed: {ex.Message}");
            }

            await TryAppendMemoryAsync(contact, userMessage.Trim(), reply).ConfigureAwait(false);

            if (_appConfig.RemoteCompanionNotifyUnreal)
                await TryNotifyUnrealAsync(userMessage.Trim(), reply).ConfigureAwait(false);

            return RemoteCompanionChatResult.Success(reply, conversationId);
        }

        public async Task<RemoteCompanionChatResult> ChatFromAudioAsync(byte[] audioBytes, string? contactIdOverride, CancellationToken cancellationToken = default)
        {
            if (audioBytes == null || audioBytes.Length == 0)
                return RemoteCompanionChatResult.Failure("audio body is empty");

            var contact = await ResolveContactAsync(contactIdOverride).ConfigureAwait(false);
            if (contact == null)
                return RemoteCompanionChatResult.Failure("No AI contact found.");

            string transcribed;
            try
            {
                transcribed = await _aiService.ProcessAudioAsync(contact, audioBytes).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return RemoteCompanionChatResult.Failure($"STT error: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(transcribed))
                return RemoteCompanionChatResult.Failure("Transcription was empty.");

            return await ChatAsync(transcribed, contactIdOverride, cancellationToken).ConfigureAwait(false);
        }

        private async Task<AIContact?> ResolveContactAsync(string? contactIdOverride)
        {
            Dictionary<string, AIContact> contacts;
            try
            {
                contacts = await _database.GetAllAsync<AIContact>().ConfigureAwait(false);
            }
            catch
            {
                return null;
            }

            if (contacts.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(contactIdOverride) && contacts.TryGetValue(contactIdOverride, out var byOverride))
                return byOverride;

            if (!string.IsNullOrWhiteSpace(_appConfig.RemoteCompanionAiContactId) &&
                contacts.TryGetValue(_appConfig.RemoteCompanionAiContactId, out var configured))
                return configured;

            return contacts.Values.FirstOrDefault(c => c.IsPrimaryAI) ?? contacts.Values.FirstOrDefault();
        }

        private async Task TryAppendMemoryAsync(AIContact contact, string userText, string reply)
        {
            if (_memoryService == null || !_appConfig.EnablePersistentMemory)
                return;

            try
            {
                var experience = $"User (remote): {userText}\nAI: {reply}\nTimestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                await _memoryService.AddMemoryAsync(contact.Id, experience).ConfigureAwait(false);

                var dataBanks = await _memoryService.GetAllDataBanksAsync().ConfigureAwait(false);
                if (dataBanks == null || string.IsNullOrWhiteSpace(contact.Name))
                    return;

                var personaDataBank = dataBanks.FirstOrDefault(db =>
                    db != null && !string.IsNullOrWhiteSpace(db.Name) && db.Name.Contains(contact.Name, StringComparison.OrdinalIgnoreCase));
                if (personaDataBank != null && !string.IsNullOrWhiteSpace(personaDataBank.Id))
                    await _memoryService.AddDataToBankAsync(personaDataBank.Id, experience).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoteCompanionChatService: memory append failed: {ex.Message}");
            }
        }

        private async Task TryNotifyUnrealAsync(string userText, string reply)
        {
            if (_virtualEnvironment == null)
                return;

            try
            {
                var status = await _virtualEnvironment.GetStatusAsync().ConfigureAwait(false);
                if (!status.IsConnected)
                    return;

                var correlationId = Guid.NewGuid().ToString("N");
                var payload = new
                {
                    type = "command",
                    payload = new
                    {
                        name = "companion_remote_exchange",
                        args = new { user = userText, assistant = reply, correlation_id = correlationId }
                    }
                };
                var json = JsonSerializer.Serialize(payload);
                await _virtualEnvironment.SendCommandAsync(json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoteCompanionChatService: Unreal notify failed: {ex.Message}");
            }
        }
    }

    public sealed class RemoteCompanionChatResult
    {
        public bool IsSuccess { get; private init; }
        public string? Error { get; private init; }
        public string? Reply { get; private init; }
        public string? ConversationId { get; private init; }

        public static RemoteCompanionChatResult Success(string reply, string conversationId) =>
            new() { IsSuccess = true, Reply = reply, ConversationId = conversationId };

        public static RemoteCompanionChatResult Failure(string error) =>
            new() { IsSuccess = false, Error = error };
    }
}
