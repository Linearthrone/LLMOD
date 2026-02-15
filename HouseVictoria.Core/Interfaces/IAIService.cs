using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Interface for AI service management and interactions
    /// </summary>
    public interface IAIService
    {
        Task<string> SendMessageAsync(AIContact contact, string message, List<ChatMessage>? context = null);
        /// <summary>Converts a short user image request into a detailed, high-quality prompt for ComfyUI/Stable Diffusion.</summary>
        Task<string> EnhanceImagePromptAsync(AIContact contact, string userImageRequest);
        Task<Stream> GenerateImageAsync(AIContact contact, string prompt);
        Task<string> ProcessImageAsync(AIContact contact, byte[] imageData, string? prompt = null);
        Task<string> ProcessAudioAsync(AIContact contact, byte[] audioData);
        Task LoadModelAsync(AIContact contact);
        Task UnloadModelAsync(AIContact contact);
        Task<bool> TestConnectionAsync(string serverUrl);
        Task<List<string>> GetAvailableModelsAsync(string serverUrl);
        Task PullModelAsync(string serverUrl, string modelName);
        event EventHandler<AIMessageEventArgs>? MessageReceived;
        event EventHandler<AIEErrorEventArgs>? ErrorOccurred;
    }

    public class AIMessageEventArgs : EventArgs
    {
        public string ContactId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class AIEErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }
}
