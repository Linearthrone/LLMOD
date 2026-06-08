using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace HouseVictoria.Services.AIServices
{
    /// <summary>
    /// Routes chat through Hermes Agent's OpenAI-compatible API (/v1/chat/completions).
    /// Hermes executes tools (terminal, browser, MCP) and returns the final assistant message.
    /// </summary>
    public class HermesAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly AppConfig _config;
        private readonly IHermesGatewayService? _gatewayService;

        public event EventHandler<AIMessageEventArgs>? MessageReceived;
        public event EventHandler<AIEErrorEventArgs>? ErrorOccurred;

        public HermesAIService(AppConfig config, IHermesGatewayService? gatewayService = null)
        {
            _config = config;
            _gatewayService = gatewayService;
            _httpClient = new HttpClient
            {
                // Tool loops (terminal, browser, MCP) can take several minutes.
                Timeout = TimeSpan.FromMinutes(15)
            };
        }

        private string BaseUrl
        {
            get
            {
                var endpoint = string.IsNullOrWhiteSpace(_config.HermesEndpoint)
                    ? "http://127.0.0.1:8642/v1"
                    : _config.HermesEndpoint.Trim().TrimEnd('/');
                if (!endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                    endpoint += "/v1";
                return endpoint;
            }
        }

        public async Task<string> SendMessageAsync(AIContact contact, string message, List<ChatMessage>? context = null)
        {
            if (_gatewayService != null)
                await _gatewayService.EnsureGatewayRunningAsync().ConfigureAwait(false);

            var messages = new List<OpenAIMessage>();

            if (!string.IsNullOrWhiteSpace(contact.SystemPrompt))
                messages.Add(new OpenAIMessage { Role = "system", Content = contact.SystemPrompt });

            if (context != null)
            {
                foreach (var msg in context)
                    messages.Add(new OpenAIMessage { Role = msg.Role, Content = msg.Content });
            }

            messages.Add(new OpenAIMessage { Role = "user", Content = message });

            var model = ResolveModelName(contact);
            var requestBody = new OpenAIChatRequest
            {
                Model = model,
                Messages = messages,
                Temperature = (float)contact.Temperature,
                MaxTokens = contact.MaxTokens > 0 ? contact.MaxTokens : 4096,
                TopP = (float)contact.TopP,
                Stream = false
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions")
                {
                    Content = JsonContent.Create(requestBody)
                };

                if (!string.IsNullOrWhiteSpace(_config.HermesApiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.HermesApiKey);

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new HttpRequestException($"Hermes API returned {response.StatusCode}: {errorContent}. Endpoint: {BaseUrl}/chat/completions");
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>().ConfigureAwait(false);
                var reply = result?.Choices?[0]?.Message?.Content ?? string.Empty;

                MessageReceived?.Invoke(this, new AIMessageEventArgs
                {
                    ContactId = contact.Id,
                    Message = reply,
                    Timestamp = DateTime.Now
                });

                return reply;
            }
            catch (TaskCanceledException ex)
            {
                var errorMsg = $"Hermes request timed out at {BaseUrl}. Long tool runs may need a higher timeout.";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs { ErrorMessage = errorMsg, Exception = ex });
                throw;
            }
            catch (HttpRequestException ex)
            {
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs { ErrorMessage = ex.Message, Exception = ex });
                throw;
            }
        }

        private string ResolveModelName(AIContact contact)
        {
            if (!string.IsNullOrWhiteSpace(_config.HermesModelName))
                return _config.HermesModelName;

            if (!string.IsNullOrWhiteSpace(contact.ModelName) &&
                !contact.ModelName.Contains('/') &&
                !contact.ModelName.Contains('\\'))
            {
                return contact.ModelName;
            }

            return "hermes-agent";
        }

        public async Task<string> EnhanceImagePromptAsync(AIContact contact, string userImageRequest)
        {
            const string systemPrompt = "You are an expert at writing detailed, effective image generation prompts. Reply with ONLY one detailed prompt—no explanations.";
            var shortContact = new AIContact
            {
                Id = contact.Id,
                Name = contact.Name,
                ModelName = ResolveModelName(contact),
                SystemPrompt = systemPrompt,
                Temperature = 0.7,
                MaxTokens = 400
            };
            return await SendMessageAsync(shortContact, userImageRequest.Trim(), null).ConfigureAwait(false);
        }

        public Task<Stream> GenerateImageAsync(AIContact contact, string prompt)
        {
            throw new NotImplementedException("Use Ollama or ComfyUI for image generation. Hermes handles tool-augmented chat.");
        }

        public Task<string> ProcessImageAsync(AIContact contact, byte[] imageData, string? prompt = null)
        {
            throw new NotImplementedException("Hermes vision via House Victoria is not implemented yet. Use Hermes gateway directly for image parts.");
        }

        public Task<string> ProcessAudioAsync(AIContact contact, byte[] audioData)
        {
            throw new NotImplementedException("Use the STT endpoint for audio transcription before sending text to Hermes.");
        }

        public Task LoadModelAsync(AIContact contact) => Task.CompletedTask;

        public Task UnloadModelAsync(AIContact contact) => Task.CompletedTask;

        public async Task<bool> TestConnectionAsync(string serverUrl)
        {
            try
            {
                var baseUrl = (string.IsNullOrWhiteSpace(serverUrl) ? BaseUrl : serverUrl.Trim().TrimEnd('/'));
                if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                    baseUrl += "/v1";

                using var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var healthBase = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                    ? baseUrl[..^3]
                    : baseUrl;
                using var health = await healthClient.GetAsync($"{healthBase}/health").ConfigureAwait(false);
                if (!health.IsSuccessStatusCode)
                    return false;

                if (string.IsNullOrWhiteSpace(_config.HermesApiKey))
                    return true;

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.HermesApiKey);
                using var models = await healthClient.SendAsync(request).ConfigureAwait(false);
                return models.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> GetAvailableModelsAsync(string serverUrl)
        {
            try
            {
                var baseUrl = (string.IsNullOrWhiteSpace(serverUrl) ? BaseUrl : serverUrl.Trim().TrimEnd('/'));
                if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                    baseUrl += "/v1";

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models");
                if (!string.IsNullOrWhiteSpace(_config.HermesApiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.HermesApiKey);

                using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return new List<string>();

                var result = await response.Content.ReadFromJsonAsync<OpenAIModelsResponse>().ConfigureAwait(false);
                return result?.Data?.Select(m => m.Id ?? string.Empty).Where(id => !string.IsNullOrEmpty(id)).ToList()
                       ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public Task PullModelAsync(string serverUrl, string modelName) => Task.CompletedTask;

        private class OpenAIMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "user";

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private class OpenAIChatRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = "hermes-agent";

            [JsonPropertyName("messages")]
            public List<OpenAIMessage> Messages { get; set; } = new();

            [JsonPropertyName("temperature")]
            public float Temperature { get; set; } = 0.7f;

            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; set; } = 4096;

            [JsonPropertyName("top_p")]
            public float TopP { get; set; } = 0.9f;

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }
        }

        private class OpenAIChoice
        {
            [JsonPropertyName("message")]
            public OpenAIMessage? Message { get; set; }
        }

        private class OpenAIChatResponse
        {
            [JsonPropertyName("choices")]
            public List<OpenAIChoice>? Choices { get; set; }
        }

        private class OpenAIModelEntry
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }
        }

        private class OpenAIModelsResponse
        {
            [JsonPropertyName("data")]
            public List<OpenAIModelEntry>? Data { get; set; }
        }
    }
}
