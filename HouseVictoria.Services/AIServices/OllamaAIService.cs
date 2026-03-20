using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

                // Automatic1111 WebUI exposes /sdapi/v1/txt2img; ComfyUI (default :8188) uses /prompt instead — try both.
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

        private async Task<Stream> GenerateImageWithComfyUIAsync(string endpoint, string promptText)
        {
            var baseUrl = endpoint.TrimEnd('/');

            // 1) Automatic1111 WebUI: POST /sdapi/v1/txt2img → { "images": [ base64, ... ] }
            var a1111Url = $"{baseUrl}/sdapi/v1/txt2img";
            var a1111Body = new
            {
                prompt = promptText,
                negative_prompt = "",
                steps = 20,
                width = 512,
                height = 512,
                cfg_scale = 7,
                seed = -1,
                sampler_index = "Euler"
            };

            using (var response = await _httpClient.PostAsJsonAsync(a1111Url, a1111Body))
            {
                var content = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var result = JsonSerializer.Deserialize<StableDiffusionResponse>(content);
                        if (result?.Images != null && result.Images.Count > 0 && !string.IsNullOrWhiteSpace(result.Images[0]))
                            return new MemoryStream(Convert.FromBase64String(result.Images[0]));
                    }
                    catch (Exception parseEx)
                    {
                        throw new Exception(
                            "Image server returned 200 but the body was not a valid Automatic1111 `txt2img` response. " +
                            $"POST {a1111Url}. Response: {TruncateForLog(content)}", parseEx);
                    }

                    throw new Exception(
                        "Automatic1111 API returned success but no `images` in the response. " +
                        $"POST {a1111Url}. Response: {TruncateForLog(content)}");
                }

                // ComfyUI (and others) often return 404/405 on /sdapi/v1/txt2img — use native ComfyUI /prompt API.
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
                {
                    return await GenerateImageViaComfyUiNativeAsync(baseUrl, promptText, width: 512, height: 512);
                }

                throw new HttpRequestException(
                    $"Image generation request failed: {(int)response.StatusCode} {response.ReasonPhrase}. " +
                    $"Tried POST {a1111Url}. Response: {TruncateForLog(content)}");
            }
        }

        /// <summary>
        /// Runs a minimal SD checkpoint txt2img graph on ComfyUI (POST /prompt, poll /history, GET /view).
        /// </summary>
        private async Task<Stream> GenerateImageViaComfyUiNativeAsync(string baseUrl, string positivePrompt, int width, int height)
        {
            var ckpt = await ResolveComfyUiCheckpointNameAsync(baseUrl);
            var seed = Random.Shared.Next(0, int.MaxValue);
            var prefix = "HouseVictoria_" + Guid.NewGuid().ToString("N")[..8];

            // API-format graph (same as ComfyUI "Save (API format)").
            var workflow = BuildComfyUiTxt2ImgWorkflow(ckpt, positivePrompt, width, height, seed, prefix);

            var clientId = Guid.NewGuid().ToString("N");
            using var promptResp = await _httpClient.PostAsJsonAsync($"{baseUrl}/prompt", new { prompt = workflow, client_id = clientId });
            var promptJson = await promptResp.Content.ReadAsStringAsync();
            if (!promptResp.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"ComfyUI POST /prompt failed: {(int)promptResp.StatusCode} {promptResp.ReasonPhrase}. Response: {TruncateForLog(promptJson)}");
            }

            using var promptDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(promptJson) ? "{}" : promptJson);
            var root = promptDoc.RootElement;

            // Some ComfyUI versions return a top-level error string/object on validation failure.
            if (root.TryGetProperty("error", out var topErr))
            {
                var errText = topErr.ValueKind == JsonValueKind.String
                    ? topErr.GetString()
                    : topErr.GetRawText();
                throw new Exception($"ComfyUI /prompt error: {TruncateForLog(errText, 4000)}");
            }

            if (root.TryGetProperty("node_errors", out var nodeErrors) &&
                nodeErrors.ValueKind == JsonValueKind.Object &&
                nodeErrors.EnumerateObject().Any())
            {
                throw new Exception(
                    "ComfyUI rejected the workflow (node_errors). Typical causes: wrong checkpoint type (e.g. Flux-only install), or custom nodes missing. " +
                    $"Details: {TruncateForLog(nodeErrors.GetRawText(), 4000)}");
            }

            var promptId = root.TryGetProperty("prompt_id", out var pid) ? pid.GetString() : null;
            if (string.IsNullOrWhiteSpace(promptId))
                throw new Exception($"ComfyUI /prompt did not return prompt_id. Response: {TruncateForLog(promptJson)}");

            System.Diagnostics.Debug.WriteLine($"[ComfyUI] Queued image gen prompt_id={promptId}, checkpoint={ckpt}, prefix={prefix}");

            var (filename, subfolder, type) = await PollComfyUiForOutputImageAsync(baseUrl, promptId);
            var viewUrl =
                $"{baseUrl}/view?filename={Uri.EscapeDataString(filename)}&subfolder={Uri.EscapeDataString(subfolder)}&type={Uri.EscapeDataString(type)}";
            using var imgResp = await _httpClient.GetAsync(viewUrl);
            var imgBytes = await imgResp.Content.ReadAsByteArrayAsync();
            if (!imgResp.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"ComfyUI GET /view failed: {(int)imgResp.StatusCode}. URL: {viewUrl}. Body: {TruncateForLog(System.Text.Encoding.UTF8.GetString(imgBytes))}");
            }

            return new MemoryStream(imgBytes);
        }

        private static Dictionary<string, object> BuildComfyUiTxt2ImgWorkflow(
            string ckptName, string positive, int width, int height, int seed, string filenamePrefix)
        {
            object Link(string nodeId, int slot) => new object[] { nodeId, slot };

            return new Dictionary<string, object>
            {
                ["4"] = new Dictionary<string, object>
                {
                    ["class_type"] = "CheckpointLoaderSimple",
                    ["inputs"] = new Dictionary<string, object> { ["ckpt_name"] = ckptName }
                },
                ["5"] = new Dictionary<string, object>
                {
                    ["class_type"] = "EmptyLatentImage",
                    ["inputs"] = new Dictionary<string, object>
                    {
                        ["width"] = width,
                        ["height"] = height,
                        ["batch_size"] = 1
                    }
                },
                ["6"] = new Dictionary<string, object>
                {
                    ["class_type"] = "CLIPTextEncode",
                    ["inputs"] = new Dictionary<string, object>
                    {
                        ["text"] = positive,
                        ["clip"] = Link("4", 1)
                    }
                },
                ["7"] = new Dictionary<string, object>
                {
                    ["class_type"] = "CLIPTextEncode",
                    ["inputs"] = new Dictionary<string, object>
                    {
                        ["text"] = "low quality, blurry, watermark, text, ugly",
                        ["clip"] = Link("4", 1)
                    }
                },
                ["3"] = new Dictionary<string, object>
                {
                    ["class_type"] = "KSampler",
                    ["inputs"] = new Dictionary<string, object>
                    {
                        ["seed"] = seed,
                        ["steps"] = 20,
                        ["cfg"] = 7,
                        ["sampler_name"] = "euler",
                        // "normal" was removed in newer ComfyUI builds; "simple" is widely supported.
                        ["scheduler"] = "simple",
                        ["denoise"] = 1.0,
                        ["model"] = Link("4", 0),
                        ["positive"] = Link("6", 0),
                        ["negative"] = Link("7", 0),
                        ["latent_image"] = Link("5", 0)
                    }
                },
                ["8"] = new Dictionary<string, object>
                {
                    ["class_type"] = "VAEDecode",
                    ["inputs"] = new Dictionary<string, object>
                    {
                        ["samples"] = Link("3", 0),
                        ["vae"] = Link("4", 2)
                    }
                },
                ["9"] = new Dictionary<string, object>
                {
                    ["class_type"] = "SaveImage",
                    ["inputs"] = new Dictionary<string, object>
                    {
                        ["filename_prefix"] = filenamePrefix,
                        ["images"] = Link("8", 0)
                    }
                }
            };
        }

        private async Task<string> ResolveComfyUiCheckpointNameAsync(string baseUrl)
        {
            try
            {
                using var r = await _httpClient.GetAsync($"{baseUrl}/models/checkpoints");
                if (r.IsSuccessStatusCode)
                {
                    var names = await r.Content.ReadFromJsonAsync<List<string>>();
                    var first = names?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
                    if (first != null)
                        return first;
                }
            }
            catch
            {
                // try object_info
            }

            using var r2 = await _httpClient.GetAsync($"{baseUrl}/object_info/CheckpointLoaderSimple");
            if (!r2.IsSuccessStatusCode)
            {
                var body = await r2.Content.ReadAsStringAsync();
                throw new Exception(
                    "Could not list ComfyUI checkpoints. Ensure ComfyUI is running and has at least one model in models/checkpoints. " +
                    $"GET /object_info/CheckpointLoaderSimple returned {(int)r2.StatusCode}. {TruncateForLog(body)}");
            }

            var json = await r2.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("CheckpointLoaderSimple", out var node))
                throw new Exception("ComfyUI object_info missing CheckpointLoaderSimple. Is this a ComfyUI server?");

            var input = node.GetProperty("input");
            var required = input.GetProperty("required");
            if (!required.TryGetProperty("ckpt_name", out var ckptEl) || ckptEl.ValueKind != JsonValueKind.Array || ckptEl.GetArrayLength() < 1)
                throw new Exception("ComfyUI returned no checkpoint list. Add a .safetensors or .ckpt to ComfyUI models/checkpoints.");

            var options = ckptEl[0];
            if (options.ValueKind != JsonValueKind.Array)
                throw new Exception("Unexpected ComfyUI ckpt_name schema.");

            foreach (var x in options.EnumerateArray())
            {
                var s = x.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }

            throw new Exception("No checkpoints found in ComfyUI. Add a Stable Diffusion checkpoint under models/checkpoints.");
        }

        private async Task<(string filename, string subfolder, string type)> PollComfyUiForOutputImageAsync(string baseUrl, string promptId)
        {
            var deadline = DateTime.UtcNow.AddMinutes(4);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(400);
                using var r = await _httpClient.GetAsync($"{baseUrl}/history/{Uri.EscapeDataString(promptId)}");
                if (!r.IsSuccessStatusCode)
                    continue;

                var json = await r.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "{}")
                    continue;

                using var doc = JsonDocument.Parse(json);
                foreach (var entry in doc.RootElement.EnumerateObject())
                {
                    var value = entry.Value;

                    // Failed runs usually have execution_error in status.messages but no (or empty) outputs — don't spin until timeout.
                    var failureDetail = TryExtractComfyUiExecutionFailure(value);
                    if (failureDetail != null)
                        throw new Exception(failureDetail);

                    if (TryExtractFirstComfyUiOutputImage(value, out var imageRef))
                        return imageRef;

                    // Finished successfully but nothing to download (misconfigured graph, preview-only, etc.)
                    if (TryGetComfyUiRunCompleted(value, out var completed) && completed)
                    {
                        throw new Exception(
                            "ComfyUI reported the workflow as completed but no image file was returned in history. " +
                            "Ensure the graph includes SaveImage and the checkpoint matches SD 1.5/SDXL (not Flux-only). " +
                            $"History: {TruncateForLog(value.GetRawText(), 2500)}");
                    }
                }
            }

            throw new Exception(
                $"ComfyUI image generation timed out waiting for outputs (prompt_id={promptId}). " +
                "If the queue is long, wait and retry; otherwise check the ComfyUI terminal for GPU/OOM errors.");
        }

        /// <summary>Parses ComfyUI /history entry: status.messages execution_error / execution_interrupted.</summary>
        private static string? TryExtractComfyUiExecutionFailure(JsonElement historyEntry)
        {
            if (!historyEntry.TryGetProperty("status", out var status))
                return null;

            if (status.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
            {
                foreach (var msg in messages.EnumerateArray())
                {
                    if (msg.ValueKind != JsonValueKind.Array || msg.GetArrayLength() < 2)
                        continue;
                    var msgType = msg[0].GetString();
                    var data = msg[1];

                    if (string.Equals(msgType, "execution_error", StringComparison.OrdinalIgnoreCase))
                    {
                        var exMsg = data.TryGetProperty("exception_message", out var em) ? em.GetString() : null;
                        var exType = data.TryGetProperty("exception_type", out var et) ? et.GetString() : null;
                        var nodeId = data.TryGetProperty("node_id", out var nid) ? nid.GetRawText() : "?";
                        var nodeType = data.TryGetProperty("node_type", out var nty) ? nty.GetString() : "?";
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(exType))
                            parts.Add(exType);
                        if (!string.IsNullOrWhiteSpace(exMsg))
                            parts.Add(exMsg);
                        parts.Add($"node {nodeType} (id {nodeId})");
                        return "ComfyUI execution failed: " + string.Join(" — ", parts);
                    }

                    if (string.Equals(msgType, "execution_interrupted", StringComparison.OrdinalIgnoreCase))
                    {
                        return "ComfyUI execution was interrupted: " + TruncateForLog(data.GetRawText(), 1500);
                    }
                }
            }

            if (status.TryGetProperty("status_str", out var sstr))
            {
                var s = sstr.GetString();
                if (string.Equals(s, "error", StringComparison.OrdinalIgnoreCase))
                {
                    return "ComfyUI reported status_str=error. " + TruncateForLog(status.GetRawText(), 2000);
                }
            }

            return null;
        }

        private static bool TryGetComfyUiRunCompleted(JsonElement historyEntry, out bool completed)
        {
            completed = false;
            if (!historyEntry.TryGetProperty("status", out var status))
                return false;
            if (!status.TryGetProperty("completed", out var c))
                return false;
            completed = c.ValueKind == JsonValueKind.True;
            return true;
        }

        private static bool TryExtractFirstComfyUiOutputImage(JsonElement historyEntry, out (string filename, string subfolder, string type) imageRef)
        {
            imageRef = default;
            if (!historyEntry.TryGetProperty("outputs", out var outputs) || outputs.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var outNode in outputs.EnumerateObject())
            {
                if (!outNode.Value.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var img in images.EnumerateArray())
                {
                    var fn = img.TryGetProperty("filename", out var f) ? f.GetString() : null;
                    if (string.IsNullOrWhiteSpace(fn))
                        continue;
                    var sf = img.TryGetProperty("subfolder", out var s) ? (s.GetString() ?? "") : "";
                    var ty = img.TryGetProperty("type", out var t) ? (t.GetString() ?? "output") : "output";
                    imageRef = (fn, sf, ty);
                    return true;
                }
            }

            return false;
        }

        private static string TruncateForLog(string? text, int maxChars = 1200)
        {
            if (string.IsNullOrEmpty(text))
                return "<empty>";
            if (text.Length <= maxChars)
                return text;
            return text.Substring(0, maxChars) + $"… (truncated, {text.Length} chars total)";
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
