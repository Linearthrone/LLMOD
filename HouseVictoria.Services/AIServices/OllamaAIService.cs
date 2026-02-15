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

        private const string EnhanceImagePromptSystemPrompt = "You are an expert at writing detailed, effective image generation prompts for ComfyUI and Stable Diffusion. " +
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
                // Get image generation endpoint from AppConfig (preferred) or environment variable
                var imageEndpoint = _appConfig?.StableDiffusionEndpoint
                    ?? Environment.GetEnvironmentVariable("STABLE_DIFFUSION_ENDPOINT")
                    ?? "http://localhost:8188"; // Default to ComfyUI port

                // If endpoint is clearly ComfyUI (port 8188 or "comfyui" in URL), use only ComfyUI (no fallback to A1111)
                var isComfyUIEndpoint = imageEndpoint.Contains("8188", StringComparison.OrdinalIgnoreCase)
                    || imageEndpoint.Contains("comfyui", StringComparison.OrdinalIgnoreCase);

                if (isComfyUIEndpoint)
                {
                    return await GenerateImageWithComfyUIAsync(imageEndpoint, prompt);
                }

                // Otherwise try ComfyUI first, then Automatic1111, then Ollama
                try
                {
                    return await GenerateImageWithComfyUIAsync(imageEndpoint, prompt);
                }
                catch (HttpRequestException)
                {
                    try
                    {
                        return await GenerateImageWithStableDiffusionAsync(imageEndpoint, prompt);
                    }
                    catch (HttpRequestException)
                    {
                        return await GenerateImageWithOllamaAsync(contact, prompt);
                    }
                }
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
            // ComfyUI uses workflow-based API with routes: /system_stats, /object_info, /prompt, /history, /view
            // (Standard ComfyUI does NOT use /api/v1/ prefix.)
            try
            {
                var baseUrl = endpoint.TrimEnd('/');

                // Check if ComfyUI is available via system_stats endpoint
                var statsResponse = await _httpClient.GetAsync($"{baseUrl}/system_stats");
                if (!statsResponse.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"ComfyUI API not available at {baseUrl}");
                }

                // Get checkpoint name from object_info (CheckpointLoaderSimple.input.required.ckpt_name)
                string? modelName = await GetComfyUICheckpointFromObjectInfoAsync(baseUrl);
                if (string.IsNullOrEmpty(modelName))
                {
                    // Fallback: try GET /models and /models/checkpoints (some ComfyUI versions)
                    modelName = await GetComfyUICheckpointFromModelsEndpointAsync(baseUrl);
                }
                if (string.IsNullOrEmpty(modelName))
                {
                    throw new Exception("No checkpoint models found in ComfyUI. Add a .safetensors model to ComfyUI/models/checkpoints/.");
                }

                // Create a basic ComfyUI workflow
                // This workflow structure connects nodes properly for text-to-image generation
                var seed = new Random().Next(0, int.MaxValue);
                var workflow = new Dictionary<string, object>
                {
                    ["1"] = new Dictionary<string, object>
                    {
                        ["inputs"] = new Dictionary<string, object>
                        {
                            ["ckpt_name"] = modelName
                        },
                        ["class_type"] = "CheckpointLoaderSimple"
                    },
                    ["2"] = new Dictionary<string, object>
                    {
                        ["inputs"] = new Dictionary<string, object>
                        {
                            ["text"] = prompt,
                            ["clip"] = new object[] { "1", 1 }
                        },
                        ["class_type"] = "CLIPTextEncode"
                    },
                    ["3"] = new Dictionary<string, object>
                    {
                        ["inputs"] = new Dictionary<string, object>
                        {
                            ["text"] = "",
                            ["clip"] = new object[] { "1", 1 }
                        },
                        ["class_type"] = "CLIPTextEncode"
                    },
                    ["4"] = new Dictionary<string, object>
                    {
                        ["inputs"] = new Dictionary<string, object>
                        {
                            ["width"] = 512,
                            ["height"] = 512,
                            ["batch_size"] = 1
                        },
                        ["class_type"] = "EmptyLatentImage"
                    },
                    ["5"] = new Dictionary<string, object>
                    {
                        ["inputs"] = new Dictionary<string, object>
                        {
                            ["seed"] = seed,
                            ["steps"] = 20,
                            ["cfg"] = 7.0,
                            ["sampler_name"] = "euler",
                            ["scheduler"] = "normal",
                            ["denoise"] = 1.0,
                            ["model"] = new object[] { "1", 0 },
                            ["positive"] = new object[] { "2", 0 },
                            ["negative"] = new object[] { "3", 0 },
                            ["latent_image"] = new object[] { "4", 0 }
                        },
                        ["class_type"] = "KSampler"
                    },
                    ["6"] = new Dictionary<string, object>
                    {
                        ["inputs"] = new Dictionary<string, object>
                        {
                            ["samples"] = new object[] { "5", 0 },
                            ["vae"] = new object[] { "1", 2 }
                        },
                        ["class_type"] = "VAEDecode"
                    },
                    ["7"] = new Dictionary<string, object>
                    {
                        ["inputs"] = new Dictionary<string, object>
                        {
                            ["filename_prefix"] = "ComfyUI",
                            ["images"] = new object[] { "6", 0 }
                        },
                        ["class_type"] = "SaveImage"
                    }
                };

                // Queue the prompt (ComfyUI uses POST /prompt, not /api/v1/prompt)
                var promptRequest = new { prompt = workflow, client_id = Guid.NewGuid().ToString("N") };
                var promptResponse = await _httpClient.PostAsJsonAsync($"{baseUrl}/prompt", promptRequest);
                if (!promptResponse.IsSuccessStatusCode)
                {
                    var errorContent = await promptResponse.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to queue ComfyUI prompt: {promptResponse.StatusCode} - {errorContent}");
                }

                var promptResult = await promptResponse.Content.ReadFromJsonAsync<JsonElement>();
                if (!promptResult.TryGetProperty("prompt_id", out var promptIdElement))
                {
                    if (promptResult.TryGetProperty("error", out var errEl))
                        throw new Exception($"ComfyUI workflow error: {errEl.GetString() ?? "Unknown"}");
                    throw new Exception("ComfyUI did not return a prompt ID");
                }

                var promptId = promptIdElement.GetString();
                if (string.IsNullOrEmpty(promptId))
                {
                    throw new Exception("ComfyUI returned an empty prompt ID");
                }

                // Poll for completion (check history endpoint)
                var maxAttempts = 120; // 2 minutes timeout for image generation
                var attempt = 0;
                while (attempt < maxAttempts)
                {
                    await Task.Delay(2000); // Wait 2 seconds between checks

                    var historyResponse = await _httpClient.GetAsync($"{baseUrl}/history/{promptId}");
                    if (historyResponse.IsSuccessStatusCode)
                    {
                        var history = await historyResponse.Content.ReadFromJsonAsync<JsonElement>();
                        if (history.TryGetProperty(promptId, out var promptHistory))
                        {
                            // Check if output exists
                            if (promptHistory.TryGetProperty("outputs", out var outputs))
                            {
                                // Get the image from outputs (usually node "7" for SaveImage)
                                if (outputs.TryGetProperty("7", out var saveNode))
                                {
                                    if (saveNode.TryGetProperty("images", out var images))
                                    {
                                        foreach (var image in images.EnumerateArray())
                                        {
                                            var filename = image.GetProperty("filename").GetString();
                                            var subfolder = image.GetProperty("subfolder").GetString() ?? "";
                                            var imageType = image.GetProperty("type").GetString() ?? "output";

                                            // Download the image
                                            var imageUrl = $"{baseUrl}/view?filename={Uri.EscapeDataString(filename ?? "")}&subfolder={Uri.EscapeDataString(subfolder ?? "")}&type={Uri.EscapeDataString(imageType ?? "output")}";
                                            var imageResponse = await _httpClient.GetAsync(imageUrl);
                                            if (imageResponse.IsSuccessStatusCode)
                                            {
                                                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
                                                return new MemoryStream(imageBytes);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    attempt++;
                }

                throw new TimeoutException("ComfyUI image generation timed out after 2 minutes");
            }
            catch (HttpRequestException)
            {
                throw; // Re-throw to allow fallback to Automatic1111
            }
            catch (Exception ex)
            {
                throw new Exception($"ComfyUI image generation failed: {ex.Message}", ex);
            }
        }

        /// <summary>Get first checkpoint name from ComfyUI object_info (CheckpointLoaderSimple.input.required.ckpt_name).</summary>
        private static async Task<string?> GetComfyUICheckpointFromObjectInfoAsync(string baseUrl, HttpClient httpClient)
        {
            var response = await httpClient.GetAsync($"{baseUrl}/object_info");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (!json.TryGetProperty("CheckpointLoaderSimple", out var node)) return null;
            if (!node.TryGetProperty("input", out var input)) return null;
            if (!input.TryGetProperty("required", out var required)) return null;
            if (!required.TryGetProperty("ckpt_name", out var ckptNameEl)) return null;
            // Format is [ ["model1.safetensors", "model2.safetensors"], "COMBO" ]
            if (ckptNameEl.ValueKind != JsonValueKind.Array || ckptNameEl.GetArrayLength() == 0) return null;
            var first = ckptNameEl.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Array)
            {
                var names = first.EnumerateArray().FirstOrDefault();
                return names.ValueKind == JsonValueKind.String ? names.GetString() : null;
            }
            if (first.ValueKind == JsonValueKind.String)
                return first.GetString();
            return null;
        }

        /// <summary>Get first checkpoint from GET /models then GET /models/checkpoints (ComfyUI standard routes).</summary>
        private static async Task<string?> GetComfyUICheckpointFromModelsEndpointAsync(string baseUrl, HttpClient httpClient)
        {
            var modelsResponse = await httpClient.GetAsync($"{baseUrl}/models");
            if (!modelsResponse.IsSuccessStatusCode) return null;
            var modelsJson = await modelsResponse.Content.ReadFromJsonAsync<JsonElement>();
            // Response can be { "checkpoints": [...], ... } or list of folder names
            if (modelsJson.TryGetProperty("checkpoints", out var list) && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0)
                return list.EnumerateArray().First().GetString();
            // Try GET /models/checkpoints
            var cpResponse = await httpClient.GetAsync($"{baseUrl}/models/checkpoints");
            if (!cpResponse.IsSuccessStatusCode) return null;
            var cpJson = await cpResponse.Content.ReadFromJsonAsync<JsonElement>();
            if (cpJson.ValueKind == JsonValueKind.Array && cpJson.GetArrayLength() > 0)
                return cpJson.EnumerateArray().First().GetString();
            if (cpJson.TryGetProperty("models", out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                return arr.EnumerateArray().First().GetString();
            return null;
        }

        private async Task<string?> GetComfyUICheckpointFromObjectInfoAsync(string baseUrl)
        {
            try { return await GetComfyUICheckpointFromObjectInfoAsync(baseUrl, _httpClient); }
            catch { return null; }
        }

        private async Task<string?> GetComfyUICheckpointFromModelsEndpointAsync(string baseUrl)
        {
            try { return await GetComfyUICheckpointFromModelsEndpointAsync(baseUrl, _httpClient); }
            catch { return null; }
        }

        private async Task<Stream> GenerateImageWithStableDiffusionAsync(string endpoint, string prompt)
        {
            // Try local Stable Diffusion (Automatic1111 webui) first: /sdapi/v1/txt2img
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
                // If local endpoint fails, try cloud API format
            }

            // Try cloud Stable Diffusion API: /api/v3/text2img
            var apiKey = Environment.GetEnvironmentVariable("STABLE_DIFFUSION_API_KEY") ?? "";
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new Exception(
                    "Stable Diffusion API key not found. " +
                    "Set STABLE_DIFFUSION_API_KEY environment variable, or use local Stable Diffusion with endpoint: http://localhost:7860");
            }

            var cloudRequestBody = new
            {
                key = apiKey,
                prompt = prompt,
                negative_prompt = "",
                width = 512,
                height = 512,
                samples = 1,
                num_inference_steps = 20
            };

            var cloudResponse = await _httpClient.PostAsJsonAsync($"{endpoint}/api/v3/text2img", cloudRequestBody);
            cloudResponse.EnsureSuccessStatusCode();

            var cloudResult = await cloudResponse.Content.ReadFromJsonAsync<StableDiffusionCloudResponse>();
            if (cloudResult?.Output != null && cloudResult.Output.Count > 0)
            {
                // Cloud API returns URLs, download the image
                var imageUrl = cloudResult.Output[0];
                var imageResponse = await _httpClient.GetAsync(imageUrl);
                imageResponse.EnsureSuccessStatusCode();
                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
                return new MemoryStream(imageBytes);
            }

            throw new Exception("Stable Diffusion API returned no images");
        }

        private async Task<Stream> GenerateImageWithOllamaAsync(AIContact contact, string prompt)
        {
            // Note: Ollama doesn't have native image generation in standard API
            // This is a placeholder for potential future support or custom models
            // For now, throw a helpful exception
            throw new NotImplementedException(
                "Ollama doesn't support image generation directly. " +
                "Please install and configure Stable Diffusion API, or use a dedicated image generation service. " +
                "Set STABLE_DIFFUSION_ENDPOINT environment variable to point to your Stable Diffusion API server.");
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
                var sttEndpoint = _appConfig?.TTSEndpoint?.Replace("/tts", "/stt").Replace("/speak", "/transcribe")
                    ?? Environment.GetEnvironmentVariable("WHISPER_ENDPOINT")
                    ?? Environment.GetEnvironmentVariable("STT_ENDPOINT")
                    ?? "http://localhost:5000/transcribe"; // Default to TTS endpoint with /transcribe path
                
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

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (result.TryGetProperty("text", out var textElement))
            {
                return textElement.GetString() ?? string.Empty;
            }
            if (result.TryGetProperty("transcription", out var transcriptionElement))
            {
                return transcriptionElement.GetString() ?? string.Empty;
            }

            // If response is plain text, return it directly
            var textResponse = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(textResponse))
            {
                return textResponse.Trim();
            }

            throw new Exception("Whisper API returned no transcription");
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
