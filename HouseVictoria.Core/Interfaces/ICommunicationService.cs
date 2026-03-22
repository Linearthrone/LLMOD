using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Interface for SMS/MMS communication services
    /// </summary>
    public interface ICommunicationService
    {
        Task<List<Contact>> GetContactsAsync();
        Task<List<Conversation>> GetConversationsAsync();
        Task RefreshContactsAsync();
        Task<Conversation> GetOrCreateConversationForContactAsync(string contactId);
        Task<List<ConversationMessage>> GetMessagesAsync(string conversationId);
        Task<ConversationMessage?> GetLastMessageAsync(string conversationId);
        Task SendMessageAsync(ConversationMessage message);
        Task SendMediaAsync(string conversationId, byte[] mediaData, string mediaType);
        Task StartVideoCallAsync(string contactId);
        Task EndVideoCallAsync(string conversationId);
        Task ShareDocumentAsync(string conversationId, string filePath);

        /// <summary>Deletes an entire conversation and all its messages.</summary>
        Task DeleteConversationAsync(string conversationId);

        /// <summary>Deletes specific messages from a conversation.</summary>
        Task DeleteMessagesAsync(string conversationId, IReadOnlyList<string> messageIds);

        /// <summary>Archives a conversation to AI long-term memory (permanent storage).</summary>
        Task ArchiveConversationAsync(string conversationId);

        event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        event EventHandler<CallStateChangedEventArgs>? CallStateChanged;
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public ConversationMessage Message { get; set; } = null!;
        public string ConversationId { get; set; } = string.Empty;
    }

    public class CallStateChangedEventArgs : EventArgs
    {
        public string ConversationId { get; set; } = string.Empty;
        public CallState State { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
