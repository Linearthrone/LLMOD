using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.CovasBridge
{
    /// <summary>
    /// Exposes an OpenAI-compatible HTTP API so COVAS: Next (Elite Dangerous) can use
    /// House Victoria's AI contact as the ship computer / second in command.
    /// </summary>
    public class OpenAICompatibleBridge : IDisposable
    {
        private readonly IAIService _aiService;
        private readonly IPersistenceService _persistenceService;
        private readonly AppConfig _appConfig;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public bool IsRunning => _listener?.IsListening ?? false;

        public OpenAICompatibleBridge(IAIService aiService, IPersistenceService persistenceService, AppConfig appConfig)
        {
            _aiService = aiService;
            _persistenceService = persistenceService;
            _appConfig = appConfig;
        }

        public Task StartAsync()
        {
            if (!(_appConfig.CovasBridgeEnabled))
                return Task.CompletedTask;

            return StartInternalAsync();
        }

        private async Task StartInternalAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_listener?.IsListening == true)
                    return;

                var prefix = NormalizePrefix(_appConfig.CovasBridgeEndpoint);
                _listener = new HttpListener();
                _listener.Prefixes.Add(prefix);

                _cts = new CancellationTokenSource();
                _listener.Start();
                _listenerTask = Task.Run(() => ListenAsync(_cts.Token));

                Debug.WriteLine($"COVAS bridge listening at {prefix} (Elite Dangerous ship computer AI)");
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await _listener!.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                { break; }
                catch (ObjectDisposedException)
                { break; }

                if (context != null)
                    _ = Task.Run(() => HandleRequestAsync(context, token), token);
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
        {
            var path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "/";
            if (string.IsNullOrEmpty(path)) path = "/";

            try
            {
                // CORS preflight
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    AddCorsHeaders(context.Response);
                    context.Response.StatusCode = 204;
                    context.Response.ContentLength64 = 0;
                    context.Response.Close();
                    return;
                }

                AddCorsHeaders(context.Response);

                if (path.Equals("/v1/models", StringComparison.OrdinalIgnoreCase) && context.Request.HttpMethod == "GET")
                {
                    await HandleListModelsAsync(context, token).ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/v1/chat/completions", StringComparison.OrdinalIgnoreCase) && context.Request.HttpMethod == "POST")
                {
                    await HandleChatCompletionsAsync(context, token).ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) || path.Equals("/", StringComparison.OrdinalIgnoreCase))
                {
                    await RespondJsonAsync(context, new { status = "ok", service = "covas-bridge" }, token).ConfigureAwait(false);
                    return;
                }

                context.Response.StatusCode = 404;
                await RespondAsync(context, "Not Found", "text/plain", token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"COVAS bridge request error: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    await RespondJsonAsync(context, new { error = new { message = ex.Message } }, token).ConfigureAwait(false);
                }
                catch { }
            }
            finally
            {
                try { context.Response.OutputStream?.Close(); } catch { }
            }
        }

        private static void AddCorsHeaders(HttpListenerResponse response)
        {
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
        }

        private async Task HandleListModelsAsync(HttpListenerContext context, CancellationToken token)
        {
            var contact = await GetShipComputerContactAsync().ConfigureAwait(false);
            var modelId = contact?.ModelName ?? "ollama";

            var payload = new
            {
                @object = "list",
                data = new[]
                {
                    new { id = modelId, @object = "model" }
                }
            };
            await RespondJsonAsync(context, payload, token).ConfigureAwait(false);
        }

        private async Task HandleChatCompletionsAsync(HttpListenerContext context, CancellationToken token)
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                body = await reader.ReadToEndAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(body))
            {
                context.Response.StatusCode = 400;
                await RespondJsonAsync(context, new { error = new { message = "Request body is required" } }, token).ConfigureAwait(false);
                return;
            }

            JsonElement root;
            try
            {
                root = JsonDocument.Parse(body).RootElement;
            }
            catch (JsonException)
            {
                context.Response.StatusCode = 400;
                await RespondJsonAsync(context, new { error = new { message = "Invalid JSON" } }, token).ConfigureAwait(false);
                return;
            }

            var messages = new List<ChatMessage>();
            if (root.TryGetProperty("messages", out var messagesProp) && messagesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in messagesProp.EnumerateArray())
                {
                    var role = m.TryGetProperty("role", out var r) ? r.GetString() ?? "user" : "user";
                    var content = m.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                    messages.Add(new ChatMessage { Role = role, Content = content });
                }
            }

            var lastUser = messages.LastOrDefault(m => m.Role == "user");
            var userText = lastUser?.Content?.Trim() ?? "";
            if (string.IsNullOrEmpty(userText))
            {
                context.Response.StatusCode = 400;
                await RespondJsonAsync(context, new { error = new { message = "No user message in messages" } }, token).ConfigureAwait(false);
                return;
            }

            var contact = await GetShipComputerContactAsync().ConfigureAwait(false);
            if (contact == null)
            {
                context.Response.StatusCode = 503;
                await RespondJsonAsync(context, new { error = new { message = "No AI contact configured for COVAS. Create an AI contact in House Victoria and set CovasContactId or use the first contact." } }, token).ConfigureAwait(false);
                return;
            }

            // Build chat context from previous messages (excluding the last user message for the call)
            List<ChatMessage>? chatContext = null;
            if (messages.Count > 1)
            {
                chatContext = messages.Take(messages.Count - 1).ToList();
            }

            string reply;
            try
            {
                reply = await _aiService.SendMessageAsync(contact, userText, chatContext).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"COVAS bridge AI error: {ex.Message}");
                context.Response.StatusCode = 502;
                await RespondJsonAsync(context, new { error = new { message = ex.Message } }, token).ConfigureAwait(false);
                return;
            }

            var modelId = root.TryGetProperty("model", out var modelProp) ? modelProp.GetString() ?? contact.ModelName : contact.ModelName;
            var response = new
            {
                id = "covas-" + Guid.NewGuid().ToString("N")[..16],
                @object = "chat.completion",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model = modelId,
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new { role = "assistant", content = reply ?? "" },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
            };
            await RespondJsonAsync(context, response, token).ConfigureAwait(false);
        }

        private async Task<AIContact?> GetShipComputerContactAsync()
        {
            var all = await _persistenceService.GetAllAsync<AIContact>().ConfigureAwait(false);
            if (all == null || all.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(_appConfig.CovasContactId))
            {
                foreach (var c in all.Values)
                {
                    if (string.Equals(c.Id, _appConfig.CovasContactId, StringComparison.OrdinalIgnoreCase))
                        return c;
                }
            }

            return all.Values.FirstOrDefault(c => c.IsPrimaryAI) ?? all.Values.First();
        }

        private static string NormalizePrefix(string endpoint)
        {
            var baseUrl = string.IsNullOrWhiteSpace(endpoint) ? "http://localhost:11435" : endpoint;
            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
                baseUrl += "/";
            return baseUrl;
        }

        private static Task RespondAsync(HttpListenerContext context, string message, string contentType, CancellationToken token)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            return context.Response.OutputStream.WriteAsync(bytes, token).AsTask();
        }

        private static Task RespondJsonAsync(HttpListenerContext context, object payload, CancellationToken token)
        {
            var json = JsonSerializer.Serialize(payload);
            return RespondAsync(context, json, "application/json", token);
        }

        public async Task StopAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                _cts?.Cancel();
                try
                {
                    if (_listener?.IsListening == true)
                        _listener.Stop();
                }
                catch { }

                if (_listenerTask != null)
                    await Task.WhenAny(_listenerTask, Task.Delay(500)).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Close(); } catch { }
            _listenerTask = null;
        }
    }
}
