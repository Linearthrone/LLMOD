using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Persona;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace HouseVictoria.Services.AIServices
{
    /// <summary>
    /// Routes chat through Hermes Agent's OpenAI-compatible API (/v1/chat/completions).
    /// Hermes executes tools (terminal, browser, MCP) and returns the final assistant message.
    /// </summary>
    public class HermesAIService : IAIService
    {
        /// <summary>Shown when Hermes returns HTTP 200 but no assistant text (e.g. LLM rate limit / empty stream).</summary>
        internal const string UnavailableSubscriberMessage =
            "The subscriber to are trying to reach is currently unavailable, please try again later.";

        private readonly HttpClient _httpClient;
        private readonly AppConfig _config;
        private readonly IHermesGatewayService? _gatewayService;
        private readonly IAgentDesktopMonitorService? _desktopMonitor;
        private readonly ICognitionThoughtStreamService? _thoughtStream;

        public event EventHandler<AIMessageEventArgs>? MessageReceived;
        public event EventHandler<AIEErrorEventArgs>? ErrorOccurred;

        public HermesAIService(
            AppConfig config,
            IHermesGatewayService? gatewayService = null,
            IAgentDesktopMonitorService? desktopMonitor = null,
            ICognitionThoughtStreamService? thoughtStream = null)
        {
            _config = config;
            _gatewayService = gatewayService;
            _desktopMonitor = desktopMonitor;
            _thoughtStream = thoughtStream;
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

            if (_desktopMonitor?.AllowComputerControl == true)
            {
                messages.Add(new OpenAIMessage
                {
                    Role = "system",
                    Content = HouseVictoriaToolCatalog.BuildHermesToolGuide(
                        ResolveGeneratedFilesPath(), includeComputerUse: true)
                });
            }

            if (context != null)
            {
                foreach (var msg in context)
                    messages.Add(new OpenAIMessage { Role = msg.Role, Content = msg.Content });
            }

            var forceBrowserCapture =
                _desktopMonitor?.AllowComputerControl == true
                && HouseVictoriaToolCatalog.IsBrowserPageRequest(message);

            var forceComputerUseScreenshot =
                _desktopMonitor?.AllowComputerControl == true
                && !forceBrowserCapture
                && HouseVictoriaToolCatalog.IsDesktopScreenshotRequest(message);

            messages.Add(new OpenAIMessage { Role = "user", Content = BuildUserContent(message) });

            var model = ResolveModelName(contact);
            var requestBody = new OpenAIChatRequest
            {
                Model = model,
                Messages = messages,
                Temperature = (float)contact.Temperature,
                MaxTokens = contact.MaxTokens > 0 ? contact.MaxTokens : 4096,
                TopP = (float)contact.TopP,
                Stream = true,
                ToolChoice = forceBrowserCapture
                    ? new Dictionary<string, object>
                    {
                        ["type"] = "function",
                        ["function"] = new Dictionary<string, string>
                        {
                            ["name"] = HouseVictoriaToolCatalog.BrowserCaptureTabToolName
                        }
                    }
                    : forceComputerUseScreenshot
                    ? new Dictionary<string, object>
                    {
                        ["type"] = "function",
                        ["function"] = new Dictionary<string, string>
                        {
                            ["name"] = HouseVictoriaToolCatalog.ComputerUseMcpToolName
                        }
                    }
                    : null
            };

            try
            {
                _desktopMonitor?.BeginSession(contact.Name);
                _thoughtStream?.NotifyChatTurnStarted(contact.Name);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions")
                {
                    Content = JsonContent.Create(requestBody)
                };

                if (!string.IsNullOrWhiteSpace(_config.HermesApiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.HermesApiKey);

                request.Headers.Accept.ParseAdd("text/event-stream");

                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new HttpRequestException($"Hermes API returned {response.StatusCode}: {errorContent}. Endpoint: {BaseUrl}/chat/completions");
                }

                var reply = await ReadStreamingReplyAsync(response).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(reply))
                    reply = UnavailableSubscriberMessage;

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
            finally
            {
                _desktopMonitor?.EndSession();
                _thoughtStream?.NotifyChatTurnEnded();
            }
        }

        private async Task<string> ReadStreamingReplyAsync(HttpResponseMessage response)
        {
            await using var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new StreamReader(body, Encoding.UTF8);

            var segment = new StringBuilder();
            var full = new StringBuilder();

            await foreach (var evt in HermesChatSseReader.ReadEventsAsync(reader).ConfigureAwait(false))
            {
                switch (evt.Kind)
                {
                    case HermesSseEventKind.ContentDelta when !string.IsNullOrEmpty(evt.Text):
                        segment.Append(evt.Text);
                        full.Append(evt.Text);
                        _thoughtStream?.NotifyStreamDelta(evt.Text, segment.ToString());
                        break;

                    case HermesSseEventKind.ToolProgress:
                        segment.Clear();
                        _thoughtStream?.NotifyHermesToolProgress(
                            evt.ToolName ?? string.Empty,
                            evt.ToolLabel ?? evt.ToolName ?? "tool",
                            evt.ToolStatus ?? "running");
                        break;

                    case HermesSseEventKind.Done:
                        return full.Length > 0 ? full.ToString() : segment.ToString();
                }
            }

            return full.Length > 0 ? full.ToString() : segment.ToString();
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

        /// <summary>
        /// Builds the user message content. When screen sharing is on, returns an OpenAI-style
        /// multimodal array (text + image_url data URL) so the vision model sees the live screen.
        /// Guidance depends on whether desktop control is allowed:
        /// - control OFF: tell the model to read the attached screenshot and NOT call computer_use.
        /// - control ON: do not forbid the tool; tell the model it MAY use computer_use to act.
        /// </summary>
        private object BuildUserContent(string message)
        {
            var sharing = _desktopMonitor?.ShareScreenWithAI == true;
            var allowControl = _desktopMonitor?.AllowComputerControl == true;
            var browserPageRequest =
                allowControl && HouseVictoriaToolCatalog.IsBrowserPageRequest(message);
            var desktopScreenshotRequest =
                allowControl && !browserPageRequest && HouseVictoriaToolCatalog.IsDesktopScreenshotRequest(message);

            var png = sharing ? _desktopMonitor?.CaptureScreenPng() : null;
            var hasImage = png != null && png.Length > 0;

            // No screen shared and no control granted → plain text, unchanged behavior.
            if (!hasImage && !allowControl)
                return message;

            string guidance;
            if (hasImage && !allowControl)
            {
                // Screen attached, control forbidden: read the image directly, don't grab the desktop.
                guidance =
                    "[A screenshot of my current screen is attached to this message. " +
                    "Look at the attached image directly to see what I'm seeing — " +
                    "do NOT call the computer_use tool or take your own screenshot. " +
                    "Reply in plain, conversational text without markdown formatting.]";
            }
            else if (browserPageRequest)
            {
                guidance =
                    $"[You MUST call {HouseVictoriaToolCatalog.BrowserCaptureTabToolName} as your first tool on this turn. " +
                    "It captures the active browser tab and returns page_map.elements with coordinates. " +
                    "Do NOT use computer_use get_screenshot for browser tabs. " +
                    "Reply in plain, conversational text without markdown formatting.]";
            }
            else if (desktopScreenshotRequest)
            {
                guidance =
                    $"[You MUST call {HouseVictoriaToolCatalog.ComputerUseMcpToolName} with action={HouseVictoriaToolCatalog.ComputerUseScreenshotAction} as your first tool " +
                    "on this turn before answering. Do NOT use vision_analyze, browser_vision, browser, " +
                    "terminal, or skill-discovery tools for this request. " +
                    "Reply in plain, conversational text without markdown formatting.]";
            }
            else if (hasImage)
            {
                // Screen attached AND control allowed: let her act on what she sees.
                guidance =
                    "[A screenshot of my current screen is attached to this message. " +
                    "Look at it directly, and you MAY use the computer_use tool " +
                    "(get_screenshot/click/type/scroll) to act on my desktop when the task needs it. " +
                    "If the browser is buried, use list_desktop_windows and focus_desktop_window first. " +
                    "Reply in plain, conversational text without markdown formatting.]";
            }
            else
            {
                // No screenshot but control allowed.
                guidance =
                    "[You MAY use the computer_use tool (get_screenshot/click/type/scroll) to act on my " +
                    "desktop when the task needs it. Stay on one browser window; use list_desktop_windows " +
                    "and focus_desktop_window if you lose it. Reply in plain, conversational text.]";
            }

            var text = string.IsNullOrWhiteSpace(message)
                ? (hasImage ? "Here is what I'm currently looking at on my screen. " : string.Empty) + guidance
                : message + "\n\n" + guidance;

            if (browserPageRequest)
            {
                text = HouseVictoriaToolCatalog.BuildBrowserCaptureMandatoryFirstAction()
                    + "\n\n"
                    + text
                    + "\n\n"
                    + HouseVictoriaToolCatalog.BuildBrowserCaptureSteering();
            }
            else if (desktopScreenshotRequest)
            {
                text = HouseVictoriaToolCatalog.BuildDesktopScreenshotMandatoryFirstAction()
                    + "\n\n"
                    + text
                    + "\n\n"
                    + HouseVictoriaToolCatalog.BuildDesktopScreenshotSteering();
            }
            else if (allowControl)
            {
                text += "\n\n" + HouseVictoriaToolCatalog.BuildComputerUseSessionSteering();
            }

            if (!hasImage)
                return text;

            var dataUrl = "data:image/png;base64," + Convert.ToBase64String(png!);
            return new object[]
            {
                new { type = "text", text },
                new { type = "image_url", image_url = new { url = dataUrl } }
            };
        }

        private string ResolveGeneratedFilesPath()
        {
            var mediaPath = _config.MediaPath ?? "Media";
            if (!Path.IsPathRooted(mediaPath))
                mediaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, mediaPath);
            return Path.Combine(mediaPath, "GeneratedFiles");
        }

        /// <summary>Extracts plain text from a string or an OpenAI multimodal content value.</summary>
        private static string ExtractText(object? content)
        {
            switch (content)
            {
                case null:
                    return string.Empty;
                case string s:
                    return s;
                case System.Text.Json.JsonElement el:
                    if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                        return el.GetString() ?? string.Empty;
                    if (el.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var parts = new List<string>();
                        foreach (var item in el.EnumerateArray())
                        {
                            if (item.ValueKind == System.Text.Json.JsonValueKind.Object
                                && item.TryGetProperty("text", out var t)
                                && t.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                parts.Add(t.GetString() ?? string.Empty);
                            }
                        }
                        return string.Join("\n", parts);
                    }
                    return el.ToString();
                default:
                    return content.ToString() ?? string.Empty;
            }
        }

        private class OpenAIMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "user";

            [JsonPropertyName("content")]
            public object Content { get; set; } = string.Empty;
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

            [JsonPropertyName("tool_choice")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public object? ToolChoice { get; set; }
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
