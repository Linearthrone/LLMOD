using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HouseVictoria.Services.AIServices
{
    /// <summary>
    /// AI Service implementation for Ollama
    /// </summary>
    public class OllamaAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _defaultEndpoint;
        private readonly AppConfig? _appConfig;

        public event EventHandler<AIMessageEventArgs>? MessageReceived;
        public event EventHandler<AIEErrorEventArgs>? ErrorOccurred;

        public OllamaAIService(string defaultEndpoint = "http://localhost:11434", AppConfig? appConfig = null)
        {
            _httpClient = new HttpClient
            {
                // Increased timeout to 5 minutes for AI responses
                // Longer responses with context and LLM parameters may take more time
                Timeout = TimeSpan.FromMinutes(5)
            };
            _defaultEndpoint = defaultEndpoint;
            _appConfig = appConfig;
        }

        public async Task<string> SendMessageAsync(AIContact contact, string message, List<ChatMessage>? context = null)
        {
            try
            {
                var endpoint = contact.ServerEndpoint;
                var messages = new List<object>();

                // Add system prompt if available
                if (!string.IsNullOrEmpty(contact.SystemPrompt))
                {
                    messages.Add(new { role = "system", content = contact.SystemPrompt });
                }

                // Add context if provided
                if (context != null)
                {
                    foreach (var msg in context)
                    {
                        messages.Add(new 
                        { 
                            role = msg.Role, 
                            content = msg.Content 
                        });
                    }
                }

                // Add current message
                messages.Add(new { role = "user", content = message });

                // Build request body with LLM parameters
                // For Ollama API, parameters are nested in an "options" object
                var optionsDict = new Dictionary<string, object>
                {
                    ["temperature"] = contact.Temperature,
                    ["top_p"] = contact.TopP,
                    ["top_k"] = contact.TopK,
                    ["repeat_penalty"] = contact.RepeatPenalty,
                    ["num_ctx"] = contact.ContextLength
                };
                
                // Only include num_predict if it's a positive value (-1 means unlimited)
                if (contact.MaxTokens > 0)
                {
                    optionsDict["num_predict"] = contact.MaxTokens;
                }

                var requestBody = new
                {
                    model = contact.ModelName,
                    messages = messages,
                    stream = false,
                    options = optionsDict
                };

                var response = await _httpClient.PostAsJsonAsync($"{endpoint}/api/chat", requestBody);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Ollama API returned {response.StatusCode}: {errorContent}. Endpoint: {endpoint}/api/chat");
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                var reply = result?.Message?.Content ?? string.Empty;
                
                if (string.IsNullOrEmpty(reply))
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Empty response from Ollama. Response: {await response.Content.ReadAsStringAsync()}");
                }

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
                var errorMsg = $"Ollama request timed out or was cancelled at {contact.ServerEndpoint}";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = errorMsg,
                    Exception = ex
                });
                throw;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                var errorMsg = $"Cannot connect to Ollama at {contact.ServerEndpoint}. Is Ollama running?";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = errorMsg,
                    Exception = ex
                });
                throw;
            }
            catch (HttpRequestException ex)
            {
                var errorMsg = $"Failed to connect to Ollama at {contact.ServerEndpoint}: {ex.Message}";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = errorMsg,
                    Exception = ex
                });
                throw;
            }
            catch (Exception ex)
            {
                var errorMsg = $"AI Service Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"{errorMsg}\nStack: {ex.StackTrace}");
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = errorMsg,
                    Exception = ex
                });
                throw;
            }
        }

        private const string EnhanceImagePromptSystemPrompt = "You are an expert at writing detailed, effective image generation prompts for ComfyUI and other diffusion-based image generators. " +
            "Given a short user request, reply with ONLY one detailed prompt—no explanations, no quotes, no preamble. " +
            "The prompt should: describe the subject clearly; add quality terms (e.g. highly detailed, sharp focus, professional); include style, lighting, and composition where helpful; " +
            "use comma-separated keywords typical of good diffusion prompts. Keep it one paragraph, under 300 words. Output nothing but the prompt.";

        public async Task<string> EnhanceImagePromptAsync(AIContact contact, string userImageRequest)
        {
            try
            {
                var endpoint = contact.ServerEndpoint;
                var messages = new List<object>
                {
                    new { role = "system", content = EnhanceImagePromptSystemPrompt },
                    new { role = "user", content = userImageRequest.Trim() }
                };
                var optionsDict = new Dictionary<string, object>
                {
                    ["temperature"] = 0.7,
                    ["top_p"] = contact.TopP,
                    ["top_k"] = contact.TopK,
                    ["repeat_penalty"] = contact.RepeatPenalty,
                    ["num_ctx"] = Math.Min(contact.ContextLength, 4096),
                    ["num_predict"] = 400
                };
                var requestBody = new { model = contact.ModelName, messages, stream = false, options = optionsDict };
                var response = await _httpClient.PostAsJsonAsync($"{endpoint}/api/chat", requestBody);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Ollama API returned {response.StatusCode}: {err}");
                }
                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                var reply = (result?.Message?.Content ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(reply))
                    return userImageRequest;
                return reply;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnhanceImagePrompt failed: {ex.Message}. Using original request.");
                return userImageRequest;
            }
        }

        public async Task<Stream> GenerateImageAsync(AIContact contact, string prompt)
        {
            try
            {
                // Get image generation endpoint from AppConfig (preferred) or environment variable.
                // This is typically a local ComfyUI instance (e.g. http://localhost:8188) and may still
                // reuse the legacy "StableDiffusionEndpoint" setting name for compatibility.
                var imageEndpoint = _appConfig?.StableDiffusionEndpoint
                    ?? Environment.GetEnvironmentVariable("STABLE_DIFFUSION_ENDPOINT")
                    ?? "http://localhost:8188"; // Default to ComfyUI

                // Use the local image generation endpoint (ComfyUI / A1111-compatible) only.
                return await GenerateImageWithComfyUIAsync(imageEndpoint, prompt);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = $"Image generation failed: {ex.Message}",
                    Exception = ex
                });
                throw;
            }
        }

        private async Task<Stream> GenerateImageWithComfyUIAsync(string endpoint, string prompt)
        {
            // Try local ComfyUI (or Automatic1111-compatible) first: /sdapi/v1/txt2img
            try
            {
                var requestBody = new
                {
                    prompt = prompt,
                    negative_prompt = "",
                    steps = 20,
                    width = 512,
                    height = 512,
                    cfg_scale = 7,
                    seed = -1,
                    sampler_index = "Euler"
                };

                var response = await _httpClient.PostAsJsonAsync($"{endpoint}/sdapi/v1/txt2img", requestBody);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<StableDiffusionResponse>();
                    if (result?.Images != null && result.Images.Count > 0)
                    {
                        // Decode base64 image
                        var imageBytes = Convert.FromBase64String(result.Images[0]);
                        return new MemoryStream(imageBytes);
                    }
                }
            }
            catch
            {
                // If local endpoint fails, fall back to generic error handling below.
            }

            throw new Exception("Image generation endpoint did not return any images. Verify that ComfyUI (or an Automatic1111-compatible server) is running and reachable.");
        }

        private async Task<Stream> GenerateImageWithOllamaAsync(AIContact contact, string prompt)
        {
            // Note: Ollama doesn't have native image generation in standard API
            // This is a placeholder for potential future support or custom models
            // For now, throw a helpful exception
            throw new NotImplementedException(
                "Ollama doesn't support image generation directly. " +
                "Please install and configure a local ComfyUI (or other Automatic1111-compatible) image generation server, " +
                "and set the image endpoint accordingly in Settings.");
        }

        private class StableDiffusionResponse
        {
            [JsonPropertyName("images")]
            public List<string>? Images { get; set; }

            [JsonPropertyName("parameters")]
            public Dictionary<string, object>? Parameters { get; set; }

            [JsonPropertyName("info")]
            public string? Info { get; set; }
        }

        private class StableDiffusionCloudResponse
        {
            [JsonPropertyName("status")]
            public string? Status { get; set; }

            [JsonPropertyName("output")]
            public List<string>? Output { get; set; }
            
            [JsonPropertyName("meta")]
            public Dictionary<string, object>? Meta { get; set; }
        }

        public async Task<string> ProcessImageAsync(AIContact contact, byte[] imageData, string? prompt = null)
        {
            try
            {
                var endpoint = contact.ServerEndpoint;
                var base64Image = Convert.ToBase64String(imageData);

                var requestBody = new
                {
                    model = contact.ModelName,
                    prompt = prompt ?? "Describe this image in detail.",
                    images = new[] { base64Image },
                    stream = false
                };

                var response = await _httpClient.PostAsJsonAsync($"{endpoint}/api/generate", requestBody);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
                return result?.Response ?? string.Empty;
            }
            catch (TaskCanceledException ex)
            {
                var errorMsg = $"Ollama image processing request timed out at {contact.ServerEndpoint}";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = errorMsg,
                    Exception = ex
                });
                throw;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                var errorMsg = $"Cannot connect to Ollama at {contact.ServerEndpoint} for image processing";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = errorMsg,
                    Exception = ex
                });
                throw;
            }
            catch (HttpRequestException ex)
            {
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
                throw;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
                throw;
            }
        }

        public async Task<string> ProcessAudioAsync(AIContact contact, byte[] audioData)
        {
            try
            {
                // Get Whisper/STT endpoint from AppConfig (preferred) or environment variable
                var sttEndpoint = (!string.IsNullOrEmpty(_appConfig?.STTEndpoint) ? _appConfig.STTEndpoint : null)
                    ?? _appConfig?.TTSEndpoint?.Replace("/tts", "/stt").Replace("/speak", "/transcribe")
                    ?? Environment.GetEnvironmentVariable("WHISPER_ENDPOINT")
                    ?? Environment.GetEnvironmentVariable("STT_ENDPOINT")
                    ?? "http://localhost:8000/transcribe"; // Default: local faster-whisper STT server
                
                // Try local Whisper API first, then cloud services
                try
                {
                    return await ProcessAudioWithWhisperAsync(sttEndpoint, audioData);
                }
                catch (HttpRequestException)
                {
                    // Whisper not available, try OpenAI Whisper API (if API key is configured)
                    var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                    if (!string.IsNullOrEmpty(openAiKey))
                    {
                        try
                        {
                            return await ProcessAudioWithOpenAIAsync(openAiKey, audioData);
                        }
                        catch (HttpRequestException)
                        {
                            // Fall through to error message
                        }
                    }
                    
                    throw new HttpRequestException(
                        "Speech-to-text service is not available. " +
                        "Please install and configure a Whisper API server, or set OPENAI_API_KEY environment variable. " +
                        "Set WHISPER_ENDPOINT or STT_ENDPOINT environment variable to point to your STT service.");
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = $"Audio processing failed: {ex.Message}",
                    Exception = ex
                });
                throw;
            }
        }

        private async Task<string> ProcessAudioWithWhisperAsync(string endpoint, byte[] audioData)
        {
            // Try Whisper API format (common local Whisper server format)
            using var content = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(audioData);
            audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            content.Add(audioContent, "audio", "audio.wav");

            var response = await _httpClient.PostAsync($"{endpoint}", content);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Whisper API returned {response.StatusCode}");
            }

            // Read once so we can handle both JSON and plain-text responses
            var textResponse = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(textResponse))
                throw new Exception("Whisper API returned no transcription");

            textResponse = textResponse.Trim();
            try
            {
                var result = JsonDocument.Parse(textResponse).RootElement;
                if (result.TryGetProperty("text", out var textElement))
                    return textElement.GetString() ?? string.Empty;
                if (result.TryGetProperty("transcription", out var transcriptionElement))
                    return transcriptionElement.GetString() ?? string.Empty;
            }
            catch (JsonException)
            {
                // Not JSON; treat as plain text transcription
            }

            return textResponse;
        }

        private async Task<string> ProcessAudioWithOpenAIAsync(string apiKey, byte[] audioData)
        {
            // OpenAI Whisper API format
            using var content = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(audioData);
            audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            content.Add(audioContent, "file", "audio.wav");
            content.Add(new StringContent("whisper-1"), "model");
            content.Add(new StringContent("text"), "response_format");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions")
            {
                Content = content
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"OpenAI API returned {response.StatusCode}");
            }

            var textResponse = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(textResponse))
            {
                return textResponse.Trim();
            }

            throw new Exception("OpenAI API returned no transcription");
        }

        public async Task LoadModelAsync(AIContact contact)
        {
            try
            {
                var endpoint = contact.ServerEndpoint;
                
                // Check if model exists by trying to get available models
                var availableModels = await GetAvailableModelsAsync(endpoint);
                if (!availableModels.Contains(contact.ModelName))
                {
                    // Model doesn't exist, try to pull it
                    await PullModelAsync(endpoint, contact.ModelName);
                }
                
                // Model is available (either already existed or was just pulled)
                contact.IsLoaded = true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = $"Failed to load model {contact.ModelName}: {ex.Message}",
                    Exception = ex
                });
                // Don't throw - mark as not loaded but continue
                contact.IsLoaded = false;
            }
        }

        public async Task PullModelAsync(string serverUrl, string modelName)
        {
            try
            {
                // Create a separate HttpClient with longer timeout for model downloads
                using var pullClient = new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(30) // Large models can take a while
                };

                var response = await pullClient.PostAsync($"{serverUrl}/api/pull", 
                    JsonContent.Create(new { name = modelName }));
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to pull model: {response.StatusCode} - {errorContent}");
                }
                
                // Wait for pull to complete (Ollama streams the pull as JSON lines)
                var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);
                string? line;
                bool pullCompleted = false;
                string? lastError = null;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        // Parse each JSON line from Ollama's stream
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(line);
                        var root = jsonDoc.RootElement;

                        // Check for status field
                        if (root.TryGetProperty("status", out var statusElement))
                        {
                            var status = statusElement.GetString();
                            
                            if (status == "success")
                            {
                                pullCompleted = true;
                                break;
                            }
                            else if (status == "error")
                            {
                                // Extract error message if available
                                if (root.TryGetProperty("error", out var errorElement))
                                {
                                    lastError = errorElement.GetString();
                                }
                                else
                                {
                                    lastError = "Unknown error during model pull";
                                }
                                throw new Exception($"Error pulling model: {lastError}");
                            }
                            // Other statuses like "pulling manifest", "downloading" are progress updates
                        }
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        // If line isn't valid JSON, skip it (might be empty or malformed)
                        continue;
                    }
                }

                // Verify the model was actually pulled by checking available models
                if (pullCompleted)
                {
                    // Wait a moment for Ollama to update its model list
                    await Task.Delay(1000);
                    
                    var availableModels = await GetAvailableModelsAsync(serverUrl);
                    // Check if model exists (with or without tag)
                    var modelExists = availableModels.Any(m => 
                        m.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                        m.StartsWith(modelName + ":", StringComparison.OrdinalIgnoreCase));
                    
                    if (!modelExists)
                    {
                        throw new Exception($"Model '{modelName}' was not found in available models after pull. The pull may have failed silently.");
                    }
                }
                else
                {
                    throw new Exception($"Model pull did not complete successfully. Last error: {lastError ?? "Unknown"}");
                }
            }
            catch (TaskCanceledException ex)
            {
                var errorMsg = $"Model pull request timed out for {modelName} from {serverUrl}";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = errorMsg,
                    Exception = ex
                });
                throw;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                var errorMsg = $"Cannot connect to Ollama at {serverUrl} to pull model {modelName}";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = errorMsg,
                    Exception = ex
                });
                throw;
            }
            catch (HttpRequestException ex)
            {
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = $"Failed to pull model {modelName} from {serverUrl}: {ex.Message}",
                    Exception = ex
                });
                throw;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = $"Error pulling model {modelName}: {ex.Message}",
                    Exception = ex
                });
                throw;
            }
        }

        public async Task UnloadModelAsync(AIContact contact)
        {
            try
            {
                var endpoint = contact.ServerEndpoint;
                var response = await _httpClient.DeleteAsync($"{endpoint}/api/generate"); // Note: depends on Ollama version
                contact.IsLoaded = false;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
            }
        }

        public async Task<bool> TestConnectionAsync(string serverUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{serverUrl}/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch (TaskCanceledException)
            {
                // Expected when server is down or timeout occurs
                return false;
            }
            catch (HttpRequestException)
            {
                // Expected when server is unreachable
                return false;
            }
            catch (System.Net.Sockets.SocketException)
            {
                // Expected when server is unreachable
                return false;
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
                var response = await _httpClient.GetAsync($"{serverUrl}/api/tags");
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMsg = $"Failed to get models from Ollama: {response.StatusCode} - {errorContent}. URL: {serverUrl}/api/tags";
                    ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                    {
                        ErrorMessage = errorMsg,
                        Exception = new HttpRequestException(errorMsg)
                    });
                    return new List<string>();
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaModelsResponse>();
                return result?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
            }
            catch (TaskCanceledException)
            {
                // Expected when server is down or timeout occurs
                return new List<string>();
            }
            catch (System.Net.Sockets.SocketException)
            {
                // Expected when server is unreachable
                return new List<string>();
            }
            catch (HttpRequestException ex)
            {
                var errorMsg = $"Cannot connect to Ollama server at {serverUrl}. Is Ollama running?";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = errorMsg,
                    Exception = ex
                });
                return new List<string>();
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error getting available models: {ex.Message}";
                ErrorOccurred?.Invoke(this, new AIEErrorEventArgs
                {
                    ErrorMessage = errorMsg,
                    Exception = ex
                });
                return new List<string>();
            }
        }

        // DTOs for Ollama API
        private class OllamaResponse
        {
            [JsonPropertyName("message")]
            public OllamaMessage? Message { get; set; }
        }

        private class OllamaMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
            
            [JsonPropertyName("role")]
            public string? Role { get; set; }
        }

        private class OllamaGenerateResponse
        {
            [JsonPropertyName("response")]
            public string Response { get; set; } = string.Empty;
        }

        private class OllamaModelsResponse
        {
            [JsonPropertyName("models")]
            public List<OllamaModel>? Models { get; set; }
        }

        private class OllamaModel
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }
    }
}
