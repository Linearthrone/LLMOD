using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace HouseVictoria.Services.AIServices
{
    /// <summary>
    /// AI Service implementation for LM Studio (OpenAI-compatible local API).
    /// Base URL is typically http://localhost:1234/v1
    /// </summary>
    public class LmStudioAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _defaultBaseUrl;

        public event EventHandler<AIMessageEventArgs>? MessageReceived;
        public event EventHandler<AIEErrorEventArgs>? ErrorOccurred;

        public LmStudioAIService(string defaultBaseUrl = "http://localhost:1234/v1")
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            _defaultBaseUrl = defaultBaseUrl.TrimEnd('/');
        }

        public async Task<string> SendMessageAsync(AIContact contact, string message, List<ChatMessage>? context = null)
        {
            var baseUrl = string.IsNullOrWhiteSpace(contact.ServerEndpoint) ? _defaultBaseUrl : contact.ServerEndpoint.TrimEnd('/');
            var messages = new List<OpenAIMessage>();

            if (!string.IsNullOrEmpty(contact.SystemPrompt))
                messages.Add(new OpenAIMessage { Role = "system", Content = contact.SystemPrompt });

            if (context != null)
            {
                foreach (var msg in context)
                    messages.Add(new OpenAIMessage { Role = msg.Role, Content = msg.Content });
            }

            messages.Add(new OpenAIMessage { Role = "user", Content = message });

            var requestBody = new OpenAIChatRequest
            {
                Model = contact.ModelName,
                Messages = messages,
                Temperature = (float)contact.Temperature,
                MaxTokens = contact.MaxTokens > 0 ? contact.MaxTokens : 2048,
                TopP = (float)contact.TopP
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/chat/completions", requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"LM Studio API returned {response.StatusCode}: {errorContent}");
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();
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
                var errorMsg = $"LM Studio request timed out at {baseUrl}";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs { ErrorMessage = errorMsg, Exception = ex });
                throw;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                var errorMsg = $"Cannot connect to LM Studio at {baseUrl}. Is LM Studio server running?";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs { ErrorMessage = errorMsg, Exception = ex });
                throw;
            }
            catch (HttpRequestException ex)
            {
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs { ErrorMessage = ex.Message, Exception = ex });
                throw;
            }
        }

        public async Task<string> EnhanceImagePromptAsync(AIContact contact, string userImageRequest)
        {
            const string systemPrompt = "You are an expert at writing detailed, effective image generation prompts. Given a short user request, reply with ONLY one detailed prompt—no explanations, no quotes. Output nothing but the prompt.";
            var baseUrl = string.IsNullOrWhiteSpace(contact.ServerEndpoint) ? _defaultBaseUrl : contact.ServerEndpoint.TrimEnd('/');
            var requestBody = new OpenAIChatRequest
            {
                Model = contact.ModelName,
                Messages = new List<OpenAIMessage>
                {
                    new OpenAIMessage { Role = "system", Content = systemPrompt },
                    new OpenAIMessage { Role = "user", Content = userImageRequest.Trim() }
                },
                Temperature = 0.7f,
                MaxTokens = 400
            };
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/chat/completions", requestBody);
                if (!response.IsSuccessStatusCode)
                    return userImageRequest;
                var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();
                var reply = (result?.Choices?[0]?.Message?.Content ?? string.Empty).Trim();
                return string.IsNullOrWhiteSpace(reply) ? userImageRequest : reply;
            }
            catch
            {
                return userImageRequest;
            }
        }

        public Task<Stream> GenerateImageAsync(AIContact contact, string prompt)
        {
            throw new NotImplementedException("LM Studio does not support image generation. Use Ollama or ComfyUI for that.");
        }

        public Task<string> ProcessImageAsync(AIContact contact, byte[] imageData, string? prompt = null)
        {
            throw new NotImplementedException("LM Studio vision is not implemented here. Use Ollama for image input.");
        }

        public Task<string> ProcessAudioAsync(AIContact contact, byte[] audioData)
        {
            throw new NotImplementedException("LM Studio does not process audio. Use Ollama or a dedicated STT endpoint.");
        }

        public Task LoadModelAsync(AIContact contact) => Task.CompletedTask;

        public Task UnloadModelAsync(AIContact contact) => Task.CompletedTask;

        public async Task<bool> TestConnectionAsync(string serverUrl)
        {
            try
            {
                var baseUrl = (string.IsNullOrWhiteSpace(serverUrl) ? _defaultBaseUrl : serverUrl).TrimEnd('/');
                var response = await _httpClient.GetAsync($"{baseUrl}/models");
                return response.IsSuccessStatusCode;
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
                var baseUrl = (string.IsNullOrWhiteSpace(serverUrl) ? _defaultBaseUrl : serverUrl).TrimEnd('/');
                var response = await _httpClient.GetAsync($"{baseUrl}/models");
                if (!response.IsSuccessStatusCode)
                    return new List<string>();
                var result = await response.Content.ReadFromJsonAsync<OpenAIModelsResponse>();
                return result?.Data?.Select(m => m.Id ?? string.Empty).Where(id => !string.IsNullOrEmpty(id)).ToList() ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public Task PullModelAsync(string serverUrl, string modelName)
        {
            // LM Studio manages models via its UI; no pull API
            return Task.CompletedTask;
        }

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
            public string Model { get; set; } = string.Empty;
            [JsonPropertyName("messages")]
            public List<OpenAIMessage> Messages { get; set; } = new();
            [JsonPropertyName("temperature")]
            public float Temperature { get; set; } = 0.7f;
            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; set; } = 2048;
            [JsonPropertyName("top_p")]
            public float TopP { get; set; } = 0.9f;
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
