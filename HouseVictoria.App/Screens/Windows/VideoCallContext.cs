namespace HouseVictoria.App.Screens.Windows
{
    public class VideoCallContext
    {
        public string ContactId { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string? ConversationId { get; set; }
        /// <summary>True when started from Phone/dialer (voice-only call). False when started from conversation (video call).</summary>
        public bool IsVoiceCall { get; set; }
    }
}
