using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.AIServices
{
    /// <summary>
    /// Routes to the primary LLM server (LM Studio, Ollama, Anything LLM, or Hermes Agent) based on config.
    /// Non-primary servers can be started manually from System Monitor.
    /// </summary>
    public class FallbackAIService : IAIService
    {
        private readonly LmStudioAIService _lmStudioService;
        private readonly OllamaAIService _ollamaService;
        private readonly HermesAIService _hermesService;
        private readonly AppConfig _config;

        public FallbackAIService(
            LmStudioAIService lmStudioService,
            OllamaAIService ollamaService,
            HermesAIService hermesService,
            AppConfig config)
        {
            _lmStudioService = lmStudioService;
            _ollamaService = ollamaService;
            _hermesService = hermesService;
            _config = config;
        }

        private string LmStudioEndpoint => (string.IsNullOrWhiteSpace(_config.LmStudioEndpoint) ? "http://localhost:1234/v1" : _config.LmStudioEndpoint).TrimEnd('/');
        private string OllamaEndpoint => (string.IsNullOrWhiteSpace(_config.OllamaEndpoint) ? "http://localhost:11434" : _config.OllamaEndpoint).TrimEnd('/');
        private string AnythingLLMEndpoint => (string.IsNullOrWhiteSpace(_config.AnythingLLMEndpoint) ? "http://localhost:3001" : _config.AnythingLLMEndpoint).TrimEnd('/');
        private string HermesEndpoint => (string.IsNullOrWhiteSpace(_config.HermesEndpoint) ? "http://127.0.0.1:8642/v1" : _config.HermesEndpoint).TrimEnd('/');
        private string PrimaryLLM => (string.IsNullOrWhiteSpace(_config.PrimaryLLM) ? "ollama" : _config.PrimaryLLM).ToLowerInvariant();

        private IAIService PrimaryService
        {
            get
            {
                return PrimaryLLM switch
                {
                    "lmstudio" => _lmStudioService,
                    "anythingllm" => _lmStudioService,
                    "hermes" => _hermesService,
                    _ => _ollamaService
                };
            }
        }

        private string PrimaryEndpoint => PrimaryLLM switch
        {
            "lmstudio" => LmStudioEndpoint,
            "anythingllm" => AnythingLLMEndpoint,
            "hermes" => HermesEndpoint,
            _ => OllamaEndpoint
        };

        private static AIContact WithEndpoint(AIContact contact, string serverEndpoint)
        {
            return new AIContact
            {
                Id = contact.Id,
                Name = contact.Name,
                ModelName = contact.ModelName,
                SystemPrompt = contact.SystemPrompt,
                ServerEndpoint = serverEndpoint,
                MCPServerEndpoint = contact.MCPServerEndpoint,
                AdditionalServers = contact.AdditionalServers,
                Temperature = contact.Temperature,
                TopP = contact.TopP,
                TopK = contact.TopK,
                RepeatPenalty = contact.RepeatPenalty,
                MaxTokens = contact.MaxTokens,
                ContextLength = contact.ContextLength
            };
        }

        private bool ShouldUseHermes(AIContact contact)
        {
            if (string.Equals(PrimaryLLM, "hermes", StringComparison.OrdinalIgnoreCase))
                return true;

            return contact.AdditionalServers.TryGetValue("hermes", out var flag) &&
                   string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
        }

        public event EventHandler<AIMessageEventArgs>? MessageReceived
        {
            add
            {
                _ollamaService.MessageReceived += value;
                _lmStudioService.MessageReceived += value;
                _hermesService.MessageReceived += value;
            }
            remove
            {
                _ollamaService.MessageReceived -= value;
                _lmStudioService.MessageReceived -= value;
                _hermesService.MessageReceived -= value;
            }
        }

        public event EventHandler<AIEErrorEventArgs>? ErrorOccurred
        {
            add
            {
                _ollamaService.ErrorOccurred += value;
                _lmStudioService.ErrorOccurred += value;
                _hermesService.ErrorOccurred += value;
            }
            remove
            {
                _ollamaService.ErrorOccurred -= value;
                _lmStudioService.ErrorOccurred -= value;
                _hermesService.ErrorOccurred -= value;
            }
        }

        public async Task<string> SendMessageAsync(AIContact contact, string message, List<ChatMessage>? context = null)
        {
            if (ShouldUseHermes(contact))
                return await _hermesService.SendMessageAsync(contact, message, context);

            var forPrimary = WithEndpoint(contact, PrimaryEndpoint);
            return await PrimaryService.SendMessageAsync(forPrimary, message, context);
        }

        public async Task<string> EnhanceImagePromptAsync(AIContact contact, string userImageRequest)
        {
            if (ShouldUseHermes(contact))
                return await _hermesService.EnhanceImagePromptAsync(contact, userImageRequest);

            var forPrimary = WithEndpoint(contact, PrimaryEndpoint);
            return await PrimaryService.EnhanceImagePromptAsync(forPrimary, userImageRequest);
        }

        public async Task<Stream> GenerateImageAsync(AIContact contact, string prompt)
        {
            try
            {
                if (PrimaryLLM == "lmstudio" || PrimaryLLM == "anythingllm")
                {
                    try
                    {
                        var forLm = WithEndpoint(contact, PrimaryEndpoint);
                        return await PrimaryService.GenerateImageAsync(forLm, prompt);
                    }
                    catch (NotImplementedException)
                    {
                        // fall through to Ollama
                    }
                }

                var forOllama = WithEndpoint(contact, OllamaEndpoint);
                return await _ollamaService.GenerateImageAsync(forOllama, prompt);
            }
            catch
            {
                var forOllama2 = WithEndpoint(contact, OllamaEndpoint);
                return await _ollamaService.GenerateImageAsync(forOllama2, prompt);
            }
        }

        public async Task<string> ProcessImageAsync(AIContact contact, byte[] imageData, string? prompt = null)
        {
            try
            {
                if (PrimaryLLM == "lmstudio" || PrimaryLLM == "anythingllm")
                {
                    try
                    {
                        var forLm = WithEndpoint(contact, PrimaryEndpoint);
                        return await PrimaryService.ProcessImageAsync(forLm, imageData, prompt);
                    }
                    catch (NotImplementedException)
                    {
                        // fall through to Ollama
                    }
                }

                var forOllama = WithEndpoint(contact, OllamaEndpoint);
                return await _ollamaService.ProcessImageAsync(forOllama, imageData, prompt);
            }
            catch
            {
                var forOllama2 = WithEndpoint(contact, OllamaEndpoint);
                return await _ollamaService.ProcessImageAsync(forOllama2, imageData, prompt);
            }
        }

        public async Task<string> ProcessAudioAsync(AIContact contact, byte[] audioData)
        {
            try
            {
                if (PrimaryLLM == "lmstudio" || PrimaryLLM == "anythingllm")
                {
                    try
                    {
                        var forLm = WithEndpoint(contact, PrimaryEndpoint);
                        return await PrimaryService.ProcessAudioAsync(forLm, audioData);
                    }
                    catch (NotImplementedException)
                    {
                        // fall through to Ollama
                    }
                }

                var forOllama = WithEndpoint(contact, OllamaEndpoint);
                return await _ollamaService.ProcessAudioAsync(forOllama, audioData);
            }
            catch
            {
                var forOllama2 = WithEndpoint(contact, OllamaEndpoint);
                return await _ollamaService.ProcessAudioAsync(forOllama2, audioData);
            }
        }

        public async Task LoadModelAsync(AIContact contact)
        {
            if (ShouldUseHermes(contact))
            {
                await _hermesService.LoadModelAsync(contact);
                return;
            }

            if (IsOpenAIEndpoint(contact.ServerEndpoint))
                await _lmStudioService.LoadModelAsync(contact);
            else
                await _ollamaService.LoadModelAsync(WithEndpoint(contact, OllamaEndpoint));
        }

        public async Task UnloadModelAsync(AIContact contact)
        {
            if (ShouldUseHermes(contact))
            {
                await _hermesService.UnloadModelAsync(contact);
                return;
            }

            if (IsOpenAIEndpoint(contact.ServerEndpoint))
                await _lmStudioService.UnloadModelAsync(contact);
            else
                await _ollamaService.UnloadModelAsync(WithEndpoint(contact, OllamaEndpoint));
        }

        public async Task<bool> TestConnectionAsync(string serverUrl)
        {
            var url = (serverUrl ?? string.Empty).TrimEnd('/');
            if (url.Equals(HermesEndpoint, StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("http://127.0.0.1:8642", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("http://localhost:8642", StringComparison.OrdinalIgnoreCase))
                return await _hermesService.TestConnectionAsync(url);

            if (url.Equals(LmStudioEndpoint, StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://localhost:1234", StringComparison.OrdinalIgnoreCase))
                return await _lmStudioService.TestConnectionAsync(url);
            if (url.Equals(AnythingLLMEndpoint, StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://localhost:3001", StringComparison.OrdinalIgnoreCase))
                return await _lmStudioService.TestConnectionAsync(url);
            return await _ollamaService.TestConnectionAsync(url);
        }

        public async Task<List<string>> GetAvailableModelsAsync(string serverUrl)
        {
            var url = (serverUrl ?? string.Empty).TrimEnd('/');
            if (url.Equals(HermesEndpoint, StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("http://127.0.0.1:8642", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("http://localhost:8642", StringComparison.OrdinalIgnoreCase))
                return await _hermesService.GetAvailableModelsAsync(url);

            if (url.Equals(LmStudioEndpoint, StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://localhost:1234", StringComparison.OrdinalIgnoreCase))
                return await _lmStudioService.GetAvailableModelsAsync(url);
            if (url.Equals(AnythingLLMEndpoint, StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://localhost:3001", StringComparison.OrdinalIgnoreCase))
                return await _lmStudioService.GetAvailableModelsAsync(url);
            return await _ollamaService.GetAvailableModelsAsync(url);
        }

        public async Task PullModelAsync(string serverUrl, string modelName)
        {
            var url = (serverUrl ?? string.Empty).TrimEnd('/');
            if (url.Equals(HermesEndpoint, StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("http://127.0.0.1:8642", StringComparison.OrdinalIgnoreCase))
            {
                await _hermesService.PullModelAsync(url, modelName);
                return;
            }

            if (url.Equals(LmStudioEndpoint, StringComparison.OrdinalIgnoreCase) || url.Equals(AnythingLLMEndpoint, StringComparison.OrdinalIgnoreCase))
                await _lmStudioService.PullModelAsync(url, modelName);
            else
                await _ollamaService.PullModelAsync(url, modelName);
        }

        private bool IsOpenAIEndpoint(string? endpoint)
        {
            var e = (endpoint ?? string.Empty).TrimEnd('/');
            return e.Equals(LmStudioEndpoint, StringComparison.OrdinalIgnoreCase) || e.StartsWith("http://localhost:1234", StringComparison.OrdinalIgnoreCase)
                || e.Equals(AnythingLLMEndpoint, StringComparison.OrdinalIgnoreCase) || e.StartsWith("http://localhost:3001", StringComparison.OrdinalIgnoreCase);
        }
    }
}
