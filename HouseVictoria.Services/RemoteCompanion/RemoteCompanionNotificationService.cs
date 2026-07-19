using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Persistence;

namespace HouseVictoria.Services.RemoteCompanion
{
    /// <summary>
    /// Pending notification feed for the Android companion poll worker (new messages + unread reminders).
    /// Watermarks are stored per contact in KeyValueStore; reminders are computed from SQLite history.
    /// </summary>
    public sealed class RemoteCompanionNotificationService : IAsyncDisposable
    {
#if DEBUG
        public static readonly TimeSpan ReminderInterval = TimeSpan.FromMinutes(1);
#else
        public static readonly TimeSpan ReminderInterval = TimeSpan.FromHours(4);
#endif

        private static readonly TimeSpan SchedulerTick = TimeSpan.FromMinutes(15);
        private const int PreviewMaxLength = 120;
        private const string WatermarkKeyPrefix = "remote-notify-watermark-";

        private readonly DatabasePersistenceService _database;
        private readonly AppConfig _appConfig;
        private readonly IPersonaContext? _personaContext;
        private PeriodicTimer? _schedulerTimer;
        private Task? _schedulerTask;
        private CancellationTokenSource? _schedulerCts;

        public RemoteCompanionNotificationService(
            DatabasePersistenceService database,
            AppConfig appConfig,
            IPersonaContext? personaContext = null)
        {
            _database = database;
            _appConfig = appConfig;
            _personaContext = personaContext;
        }

        public void StartReminderScheduler()
        {
            if (_schedulerTask != null)
                return;

            _schedulerCts = new CancellationTokenSource();
            _schedulerTimer = new PeriodicTimer(SchedulerTick);
            _schedulerTask = RunSchedulerAsync(_schedulerCts.Token);
        }

        private async Task RunSchedulerAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (_schedulerTimer != null &&
                       await _schedulerTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    // Reminders are exposed dynamically via GET pending; tick keeps the loop alive for future hooks.
                    _ = await GetPendingAsync(null, null, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoteCompanionNotificationService scheduler: {ex.Message}");
            }
        }

        public async Task<RemotePendingNotificationsResponse> GetPendingAsync(
            DateTime? sinceUtc,
            string? contactIdFilter,
            CancellationToken cancellationToken = default)
        {
            var contacts = await ResolveContactsAsync(contactIdFilter, cancellationToken).ConfigureAwait(false);
            var items = new List<RemotePendingNotificationItem>();

            foreach (var contact in contacts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var conversationId = $"conv-{contact.Id}";
                var messages = await _database.GetMessagesAsync(conversationId, 200).ConfigureAwait(false);
                if (messages.Count == 0)
                    continue;

                var watermark = await GetWatermarkAsync(contact.Id).ConfigureAwait(false);
                var startIndex = ResolveStartIndex(messages, watermark?.LastSeenMessageId);

                foreach (var message in messages.Skip(startIndex))
                {
                    if (message.Direction != MessageDirection.Incoming)
                        continue;
                    if (sinceUtc.HasValue && message.Timestamp.ToUniversalTime() < sinceUtc.Value)
                        continue;

                    items.Add(BuildItem(contact, message, RemotePendingNotificationKind.NewMessage));
                }

                var last = messages[^1];
                if (last.Direction == MessageDirection.Incoming &&
                    DateTime.UtcNow - last.Timestamp.ToUniversalTime() >= ReminderInterval &&
                    !string.Equals(watermark?.AckReminderForMessageId, last.Id, StringComparison.Ordinal))
                {
                    if (!sinceUtc.HasValue || last.Timestamp.ToUniversalTime() >= sinceUtc.Value)
                    {
                        items.Add(BuildItem(contact, last, RemotePendingNotificationKind.UnreadReminder));
                    }
                }
            }

            return new RemotePendingNotificationsResponse { Items = items };
        }

        public async Task AckAsync(RemoteNotificationAckRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.ContactId))
                throw new ArgumentException("contactId is required", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();
            var watermark = await GetWatermarkAsync(request.ContactId).ConfigureAwait(false)
                            ?? new RemoteNotificationWatermark();

            if (!string.IsNullOrWhiteSpace(request.LastSeenMessageId))
                watermark.LastSeenMessageId = request.LastSeenMessageId;

            if (!string.IsNullOrWhiteSpace(request.AckReminderForMessageId))
                watermark.AckReminderForMessageId = request.AckReminderForMessageId;

            await SaveWatermarkAsync(request.ContactId, watermark).ConfigureAwait(false);
        }

        private static int ResolveStartIndex(IReadOnlyList<ConversationMessage> messages, string? lastSeenMessageId)
        {
            if (string.IsNullOrWhiteSpace(lastSeenMessageId))
                return 0;

            for (var i = 0; i < messages.Count; i++)
            {
                if (string.Equals(messages[i].Id, lastSeenMessageId, StringComparison.Ordinal))
                    return i + 1;
            }

            return 0;
        }

        private static RemotePendingNotificationItem BuildItem(
            AIContact contact,
            ConversationMessage message,
            RemotePendingNotificationKind kind)
        {
            var preview = BuildPreview(message);
            if (kind == RemotePendingNotificationKind.UnreadReminder)
            {
                preview = string.IsNullOrWhiteSpace(contact.Name)
                    ? "You haven't replied yet"
                    : $"{contact.Name}: You haven't replied yet";
            }

            return new RemotePendingNotificationItem
            {
                ContactId = contact.Id,
                ContactName = contact.Name,
                MessageId = message.Id,
                Preview = preview,
                CreatedAt = message.Timestamp.ToUniversalTime(),
                Kind = kind
            };
        }

        private static string BuildPreview(ConversationMessage message)
        {
            if (message.Type != MessageType.Text || string.IsNullOrWhiteSpace(message.Content))
                return "Sent you a message";

            var text = message.Content.Trim();
            if (text.Length <= PreviewMaxLength)
                return text;

            return text[..PreviewMaxLength] + "…";
        }

        private async Task<IReadOnlyList<AIContact>> ResolveContactsAsync(
            string? contactIdFilter,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(contactIdFilter))
            {
                var single = await ResolveContactAsync(contactIdFilter).ConfigureAwait(false);
                return single != null ? new[] { single } : Array.Empty<AIContact>();
            }

            Dictionary<string, AIContact> contacts;
            try
            {
                contacts = await _database.GetAllAsync<AIContact>().ConfigureAwait(false);
            }
            catch
            {
                return Array.Empty<AIContact>();
            }

            if (contacts.Count == 0)
                return Array.Empty<AIContact>();

            var preferredId = _appConfig.RemoteCompanionAiContactId;
            if (!string.IsNullOrWhiteSpace(preferredId) && contacts.ContainsKey(preferredId))
            {
                return new[] { contacts[preferredId] };
            }

            var list = contacts.Values.ToList();
            list.Sort((a, b) => b.LastUsedAt.CompareTo(a.LastUsedAt));
            return list;
        }

        private async Task<AIContact?> ResolveContactAsync(string contactId)
        {
            if (_personaContext != null)
                return await _personaContext.ResolveAsync(contactId).ConfigureAwait(false);

            Dictionary<string, AIContact> contacts;
            try
            {
                contacts = await _database.GetAllAsync<AIContact>().ConfigureAwait(false);
            }
            catch
            {
                return null;
            }

            return contacts.Values.FirstOrDefault(c => string.Equals(c.Id, contactId, StringComparison.Ordinal));
        }

        private static string WatermarkKey(string contactId) => WatermarkKeyPrefix + contactId;

        private Task<RemoteNotificationWatermark?> GetWatermarkAsync(string contactId) =>
            _database.GetAsync<RemoteNotificationWatermark>(WatermarkKey(contactId));

        private Task SaveWatermarkAsync(string contactId, RemoteNotificationWatermark watermark) =>
            _database.SetAsync(WatermarkKey(contactId), watermark);

        public async ValueTask DisposeAsync()
        {
            if (_schedulerCts != null)
            {
                await _schedulerCts.CancelAsync().ConfigureAwait(false);
                _schedulerCts.Dispose();
                _schedulerCts = null;
            }

            _schedulerTimer?.Dispose();
            _schedulerTimer = null;

            if (_schedulerTask != null)
            {
                try
                {
                    await _schedulerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // expected
                }

                _schedulerTask = null;
            }
        }
    }

    public sealed class RemotePendingNotificationsResponse
    {
        public IReadOnlyList<RemotePendingNotificationItem> Items { get; init; } = Array.Empty<RemotePendingNotificationItem>();
    }

    public sealed class RemotePendingNotificationItem
    {
        public string ContactId { get; init; } = string.Empty;
        public string ContactName { get; init; } = string.Empty;
        public string MessageId { get; init; } = string.Empty;
        public string Preview { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public RemotePendingNotificationKind Kind { get; init; }
    }

    public enum RemotePendingNotificationKind
    {
        NewMessage,
        UnreadReminder
    }

    public sealed class RemoteNotificationAckRequest
    {
        public string ContactId { get; set; } = string.Empty;
        public string? LastSeenMessageId { get; set; }
        public string? AckReminderForMessageId { get; set; }
    }

    public sealed class RemoteNotificationWatermark
    {
        public string? LastSeenMessageId { get; set; }
        public string? AckReminderForMessageId { get; set; }
    }
}
