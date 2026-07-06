using System.Text.Json;
using System.IO;
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
        private readonly IVictoriaEmbodimentService? _embodiment;
        private readonly AppConfig _appConfig;
        private readonly IPersonaContext? _personaContext;

        public RemoteCompanionChatService(
            IAIService aiService,
            DatabasePersistenceService database,
            IMemoryService? memoryService,
            IVirtualEnvironmentService? virtualEnvironment,
            AppConfig appConfig,
            IPersonaContext? personaContext = null,
            IVictoriaEmbodimentService? embodiment = null)
        {
            _aiService = aiService;
            _database = database;
            _memoryService = memoryService;
            _virtualEnvironment = virtualEnvironment;
            _appConfig = appConfig;
            _personaContext = personaContext;
            _embodiment = embodiment;
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
            {
                if (_embodiment != null)
                    await _embodiment.OnChatExchangeAsync(contact.Id, userMessage.Trim(), reply, cancellationToken).ConfigureAwait(false);
                else
                    await TryNotifyUnrealAsync(userMessage.Trim(), reply).ConfigureAwait(false);
            }

            return RemoteCompanionChatResult.Success(reply, conversationId);
        }

        public async Task<IReadOnlyList<RemoteContactSummary>> ListContactsAsync(CancellationToken cancellationToken = default)
        {
            Dictionary<string, AIContact> contacts;
            try
            {
                contacts = await _database.GetAllAsync<AIContact>().ConfigureAwait(false);
            }
            catch
            {
                return Array.Empty<RemoteContactSummary>();
            }

            if (contacts.Count == 0)
                return Array.Empty<RemoteContactSummary>();

            var summaries = new List<RemoteContactSummary>(contacts.Count);
            foreach (var contact in contacts.Values.OrderByDescending(c => c.LastUsedAt))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var conversationId = $"conv-{contact.Id}";
                var messages = await _database.GetMessagesAsync(conversationId, 1).ConfigureAwait(false);
                var last = messages.OrderByDescending(m => m.Timestamp).FirstOrDefault();
                var preview = last?.Content;
                if (!string.IsNullOrEmpty(preview) && preview.Length > 140)
                    preview = preview[..140] + "…";

                summaries.Add(new RemoteContactSummary
                {
                    Id = contact.Id,
                    Name = contact.Name,
                    Description = contact.Description,
                    IsPrimary = contact.IsPrimaryAI,
                    HasAvatar = HasAvatarFile(contact.AvatarUrl),
                    LastMessagePreview = preview,
                    LastMessageAt = last?.Timestamp ?? contact.LastUsedAt
                });
            }

            return summaries;
        }

        public async Task<IReadOnlyList<RemoteMessageDto>> GetContactMessagesAsync(
            string contactId,
            int limit = 60,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(contactId))
                return Array.Empty<RemoteMessageDto>();

            var contact = await ResolveContactAsync(contactId).ConfigureAwait(false);
            if (contact == null)
                return Array.Empty<RemoteMessageDto>();

            var conversationId = $"conv-{contact.Id}";
            var messages = await _database.GetMessagesAsync(conversationId, Math.Clamp(limit, 1, 200))
                .ConfigureAwait(false);

            return messages
                .Where(m => m.Type == MessageType.Text)
                .OrderBy(m => m.Timestamp)
                .Select(m => new RemoteMessageDto
                {
                    Id = m.Id,
                    Role = m.Direction == MessageDirection.Outgoing ? "user" : "assistant",
                    Content = m.Content,
                    Timestamp = m.Timestamp
                })
                .ToList();
        }

        public async Task<(string Path, string ContentType)?> TryGetAvatarAsync(string contactId)
        {
            var contact = await ResolveContactAsync(contactId).ConfigureAwait(false);
            if (contact == null || !HasAvatarFile(contact.AvatarUrl))
                return null;

            var path = contact.AvatarUrl!.Trim();
            var contentType = GuessImageContentType(path);
            return (path, contentType);
        }

        private static bool HasAvatarFile(string? avatarUrl) =>
            !string.IsNullOrWhiteSpace(avatarUrl) && File.Exists(avatarUrl.Trim());

        private static string GuessImageContentType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/png"
            };
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
            var preferredId = !string.IsNullOrWhiteSpace(contactIdOverride)
                ? contactIdOverride
                : _appConfig.RemoteCompanionAiContactId;

            if (_personaContext != null)
                return await _personaContext.ResolveAsync(preferredId).ConfigureAwait(false);

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

            if (!string.IsNullOrWhiteSpace(preferredId))
            {
                var preferred = contacts.Values.FirstOrDefault(c => string.Equals(c.Id, preferredId, StringComparison.Ordinal));
                if (preferred != null)
                    return preferred;
            }

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

    public sealed class RemoteContactSummary
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public bool IsPrimary { get; init; }
        public bool HasAvatar { get; init; }
        public string? LastMessagePreview { get; init; }
        public DateTime LastMessageAt { get; init; }
    }

    public sealed class RemoteMessageDto
    {
        public string Id { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
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
