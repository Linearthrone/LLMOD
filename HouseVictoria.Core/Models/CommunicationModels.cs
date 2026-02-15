namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// Represents a contact (human or AI)
    /// </summary>
    public class Contact
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? AvatarUrl { get; set; }
        public ContactType Type { get; set; } = ContactType.Human;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Conversation history with a contact
    /// </summary>
    public class Conversation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ContactId { get; set; } = string.Empty;
        public CallState CallState { get; set; } = CallState.None;
        public DateTime LastMessageAt { get; set; } = DateTime.Now;
        public int UnreadCount { get; set; } = 0;
    }

    /// <summary>
    /// Individual message in a conversation
    /// </summary>
    public class ConversationMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ConversationId { get; set; } = string.Empty;
        public MessageDirection Direction { get; set; }
        public MessageType Type { get; set; } = MessageType.Text;
        public string Content { get; set; } = string.Empty;
        public byte[]? MediaData { get; set; }
        public string? MediaType { get; set; }
        public string? FilePath { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
    }

    public enum ContactType
    {
        Human,
        AI
    }

    public enum CallState
    {
        None,
        Incoming,
        Outgoing,
        Connected,
        Ended,
        Missed
    }

    public enum MessageDirection
    {
        Incoming,
        Outgoing
    }

    public enum MessageType
    {
        Text,
        Image,
        Video,
        Audio,
        Document
    }
}
