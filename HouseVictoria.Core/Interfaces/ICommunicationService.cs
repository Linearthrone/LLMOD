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
        Task<List<ConversationMessage>> GetMessagesAsync(string conversationId);
        Task<ConversationMessage?> GetLastMessageAsync(string conversationId);
        Task SendMessageAsync(ConversationMessage message);
        Task SendMediaAsync(string conversationId, byte[] mediaData, string mediaType);
        Task StartVideoCallAsync(string contactId);
        Task EndVideoCallAsync(string conversationId);
        Task ShareDocumentAsync(string conversationId, string filePath);
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
