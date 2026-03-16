using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.AIServices
{
    /// <summary>
    /// Wraps two AI services: tries the primary (e.g. LM Studio) first, then falls back to the backup (e.g. Ollama) on failure.
    /// </summary>
    public class FallbackAIService : IAIService
    {
        private readonly IAIService _primary;
        private readonly IAIService _backup;
        private readonly AppConfig _config;

        public FallbackAIService(IAIService primary, IAIService backup, AppConfig config)
        {
            _primary = primary;
            _backup = backup;
            _config = config;
        }

        private string LmStudioEndpoint => (string.IsNullOrWhiteSpace(_config.LmStudioEndpoint) ? "http://localhost:1234/v1" : _config.LmStudioEndpoint).TrimEnd('/');
        private string OllamaEndpoint => (string.IsNullOrWhiteSpace(_config.OllamaEndpoint) ? "http://localhost:11434" : _config.OllamaEndpoint).TrimEnd('/');
        private bool UseLmStudioFirst => _config.UseLmStudioAsPrimary;

        private static AIContact WithEndpoint(AIContact contact, string serverEndpoint)
        {
            var c = new AIContact
            {
                Id = contact.Id,
                Name = contact.Name,
                ModelName = contact.ModelName,
                SystemPrompt = contact.SystemPrompt,
                ServerEndpoint = serverEndpoint,
                MCPServerEndpoint = contact.MCPServerEndpoint,
                Temperature = contact.Temperature,
                TopP = contact.TopP,
                TopK = contact.TopK,
                RepeatPenalty = contact.RepeatPenalty,
                MaxTokens = contact.MaxTokens,
                ContextLength = contact.ContextLength
            };
            return c;
        }

        public event EventHandler<AIMessageEventArgs>? MessageReceived
        {
            add { _backup.MessageReceived += value; _primary.MessageReceived += value; }
            remove { _backup.MessageReceived -= value; _primary.MessageReceived -= value; }
        }

        public event EventHandler<AIEErrorEventArgs>? ErrorOccurred
        {
            add { _backup.ErrorOccurred += value; _primary.ErrorOccurred += value; }
            remove { _backup.ErrorOccurred -= value; _primary.ErrorOccurred -= value; }
        }

        public async Task<string> SendMessageAsync(AIContact contact, string message, List<ChatMessage>? context = null)
        {
            if (UseLmStudioFirst)
            {
                try
                {
                    var forLm = WithEndpoint(contact, LmStudioEndpoint);
                    return await _primary.SendMessageAsync(forLm, message, context);
                }
                catch
                {
                    var forOllama = WithEndpoint(contact, OllamaEndpoint);
                    return await _backup.SendMessageAsync(forOllama, message, context);
                }
            }
            var forBackup = WithEndpoint(contact, OllamaEndpoint);
            return await _backup.SendMessageAsync(forBackup, message, context);
        }

        public async Task<string> EnhanceImagePromptAsync(AIContact contact, string userImageRequest)
        {
            if (UseLmStudioFirst)
            {
                try
                {
                    var forLm = WithEndpoint(contact, LmStudioEndpoint);
                    return await _primary.EnhanceImagePromptAsync(forLm, userImageRequest);
                }
                catch
                {
                    var forOllama = WithEndpoint(contact, OllamaEndpoint);
                    return await _backup.EnhanceImagePromptAsync(forOllama, userImageRequest);
                }
            }
            var forOllama2 = WithEndpoint(contact, OllamaEndpoint);
            return await _backup.EnhanceImagePromptAsync(forOllama2, userImageRequest);
        }

        public async Task<Stream> GenerateImageAsync(AIContact contact, string prompt)
        {
            try
            {
                if (UseLmStudioFirst)
                {
                    try
                    {
                        var forLm = WithEndpoint(contact, LmStudioEndpoint);
                        return await _primary.GenerateImageAsync(forLm, prompt);
                    }
                    catch (NotImplementedException)
                    {
                        // LM Studio doesn't support image gen; fall through to Ollama
                    }
                }
                var forOllama = WithEndpoint(contact, OllamaEndpoint);
                return await _backup.GenerateImageAsync(forOllama, prompt);
            }
            catch
            {
                var forOllama2 = WithEndpoint(contact, OllamaEndpoint);
                return await _backup.GenerateImageAsync(forOllama2, prompt);
            }
        }

        public async Task<string> ProcessImageAsync(AIContact contact, byte[] imageData, string? prompt = null)
        {
            try
            {
                if (UseLmStudioFirst)
                {
                    try
                    {
                        var forLm = WithEndpoint(contact, LmStudioEndpoint);
                        return await _primary.ProcessImageAsync(forLm, imageData, prompt);
                    }
                    catch (NotImplementedException)
                    {
                        // fall through to Ollama
                    }
                }
                var forOllama = WithEndpoint(contact, OllamaEndpoint);
                return await _backup.ProcessImageAsync(forOllama, imageData, prompt);
            }
            catch
            {
                var forOllama2 = WithEndpoint(contact, OllamaEndpoint);
                return await _backup.ProcessImageAsync(forOllama2, imageData, prompt);
            }
        }

        public async Task<string> ProcessAudioAsync(AIContact contact, byte[] audioData)
        {
            try
            {
                if (UseLmStudioFirst)
                {
                    try
                    {
                        var forLm = WithEndpoint(contact, LmStudioEndpoint);
                        return await _primary.ProcessAudioAsync(forLm, audioData);
                    }
                    catch (NotImplementedException)
                    {
                        // fall through to Ollama
                    }
                }
                var forOllama = WithEndpoint(contact, OllamaEndpoint);
                return await _backup.ProcessAudioAsync(forOllama, audioData);
            }
            catch
            {
                var forOllama2 = WithEndpoint(contact, OllamaEndpoint);
                return await _backup.ProcessAudioAsync(forOllama2, audioData);
            }
        }

        public async Task LoadModelAsync(AIContact contact)
        {
            if (IsLmStudioEndpoint(contact.ServerEndpoint))
                await _primary.LoadModelAsync(contact);
            else
                await _backup.LoadModelAsync(WithEndpoint(contact, OllamaEndpoint));
        }

        public async Task UnloadModelAsync(AIContact contact)
        {
            if (IsLmStudioEndpoint(contact.ServerEndpoint))
                await _primary.UnloadModelAsync(contact);
            else
                await _backup.UnloadModelAsync(WithEndpoint(contact, OllamaEndpoint));
        }

        public async Task<bool> TestConnectionAsync(string serverUrl)
        {
            var url = (serverUrl ?? string.Empty).TrimEnd('/');
            if (url.Equals(LmStudioEndpoint, StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://localhost:1234", StringComparison.OrdinalIgnoreCase))
                return await _primary.TestConnectionAsync(url);
            return await _backup.TestConnectionAsync(url);
        }

        public async Task<List<string>> GetAvailableModelsAsync(string serverUrl)
        {
            var url = (serverUrl ?? string.Empty).TrimEnd('/');
            if (url.Equals(LmStudioEndpoint, StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://localhost:1234", StringComparison.OrdinalIgnoreCase))
                return await _primary.GetAvailableModelsAsync(url);
            return await _backup.GetAvailableModelsAsync(url);
        }

        public async Task PullModelAsync(string serverUrl, string modelName)
        {
            var url = (serverUrl ?? string.Empty).TrimEnd('/');
            if (url.Equals(LmStudioEndpoint, StringComparison.OrdinalIgnoreCase))
                await _primary.PullModelAsync(url, modelName);
            else
                await _backup.PullModelAsync(url, modelName);
        }

        private bool IsLmStudioEndpoint(string? endpoint)
        {
            var e = (endpoint ?? string.Empty).TrimEnd('/');
            return e.Equals(LmStudioEndpoint, StringComparison.OrdinalIgnoreCase) || e.StartsWith("http://localhost:1234", StringComparison.OrdinalIgnoreCase);
        }
    }
}
