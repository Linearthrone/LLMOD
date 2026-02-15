using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Speech.Synthesis;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HouseVictoria.Services.TTS
{
    /// <summary>
    /// Lightweight HTTP host that exposes Windows TTS as a local service.
    /// Used as a fallback when no external TTS server is available.
    /// </summary>
    public class LocalTtsHttpHost : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly SpeechSynthesizer _synthesizer;
        private readonly SemaphoreSlim _synthLock = new(1, 1);
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private readonly string _prefix;

        public LocalTtsHttpHost(string endpoint)
        {
            _prefix = NormalizePrefix(endpoint);
            _synthesizer = new SpeechSynthesizer();
        }

        public bool IsRunning => _listener.IsListening;

        public Task StartAsync()
        {
            if (_listener.IsListening)
                return Task.CompletedTask;

            _listener.Prefixes.Clear();
            _listener.Prefixes.Add(_prefix);

            _cts = new CancellationTokenSource();
            _listener.Start();
            _listenerTask = Task.Run(() => ListenAsync(_cts.Token));

            return Task.CompletedTask;
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (context != null)
                {
                    _ = Task.Run(() => HandleRequestAsync(context, token), token);
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
        {
            try
            {
                var path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "/";
                if (string.IsNullOrEmpty(path))
                    path = "/";

                if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) || path.Equals("/", StringComparison.OrdinalIgnoreCase))
                {
                    await RespondAsync(context, "ok", "text/plain", token).ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/voices", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/api/voices", StringComparison.OrdinalIgnoreCase))
                {
                    var voices = _synthesizer.GetInstalledVoices().Select(v => v.VoiceInfo.Name).ToArray();
                    await RespondJsonAsync(context, voices, token).ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/tts", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/api/tts", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/api/synthesize", StringComparison.OrdinalIgnoreCase))
                {
                    var request = await ParseTtsRequestAsync(context.Request, token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(request.Text))
                    {
                        context.Response.StatusCode = 400;
                        await RespondAsync(context, "Missing text", "text/plain", token).ConfigureAwait(false);
                        return;
                    }

                    var audio = await SynthesizeAsync(request.Text, request.Voice, request.Speed, token).ConfigureAwait(false);
                    if (audio == null)
                    {
                        context.Response.StatusCode = 500;
                        await RespondAsync(context, "TTS failed", "text/plain", token).ConfigureAwait(false);
                        return;
                    }

                    context.Response.ContentType = "audio/wav";
                    context.Response.ContentLength64 = audio.Length;
                    await context.Response.OutputStream.WriteAsync(audio, 0, audio.Length, token).ConfigureAwait(false);
                    return;
                }

                context.Response.StatusCode = 404;
                await RespondAsync(context, "Not Found", "text/plain", token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalTtsHttpHost request error: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                }
                catch { }
            }
            finally
            {
                try
                {
                    context.Response.OutputStream.Close();
                }
                catch { }
            }
        }

        private async Task<TtsRequest> ParseTtsRequestAsync(HttpListenerRequest request, CancellationToken token)
        {
            var text = string.Empty;
            string? voice = null;
            float speed = 1.0f;

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                var query = request.Url?.Query ?? string.Empty;
                if (query.StartsWith("?"))
                    query = query.Substring(1);

                foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kvp = part.Split('=', 2);
                    var key = Uri.UnescapeDataString(kvp[0]);
                    var value = kvp.Length > 1 ? Uri.UnescapeDataString(kvp[1]) : string.Empty;

                    if (key.Equals("text", StringComparison.OrdinalIgnoreCase))
                        text = value;
                    else if (key.Equals("voice", StringComparison.OrdinalIgnoreCase))
                        voice = value;
                    else if (key.Equals("speed", StringComparison.OrdinalIgnoreCase) && float.TryParse(value, out var parsedSpeed))
                        speed = parsedSpeed;
                }
            }
            else if (request.HasEntityBody)
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        using var json = JsonDocument.Parse(body);
                        var root = json.RootElement;
                        if (root.TryGetProperty("text", out var textProp))
                            text = textProp.GetString() ?? string.Empty;
                        if (root.TryGetProperty("voice", out var voiceProp))
                            voice = voiceProp.GetString();
                        if (root.TryGetProperty("speed", out var speedProp) && speedProp.ValueKind == JsonValueKind.Number)
                            speed = (float)speedProp.GetDouble();
                    }
                    catch (JsonException)
                    {
                        // Ignore invalid JSON and fall back to defaults
                    }
                }
            }

            return new TtsRequest(text, voice, speed);
        }

        private async Task<byte[]?> SynthesizeAsync(string text, string? voice, float speed, CancellationToken token)
        {
            await _synthLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!string.IsNullOrWhiteSpace(voice))
                {
                    try
                    {
                        _synthesizer.SelectVoice(voice);
                    }
                    catch
                    {
                        // Ignore selection errors and use default voice
                    }
                }

                var clampedSpeed = Math.Clamp(speed, 0.5f, 2.0f);
                var rate = (int)((clampedSpeed - 1.0f) * 10);
                rate = Math.Max(-10, Math.Min(10, rate));
                _synthesizer.Rate = rate;

                using var memoryStream = new MemoryStream();
                _synthesizer.SetOutputToWaveStream(memoryStream);
                _synthesizer.Speak(text);
                _synthesizer.SetOutputToDefaultAudioDevice();
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalTtsHttpHost synthesis error: {ex.Message}");
                return null;
            }
            finally
            {
                _synthLock.Release();
            }
        }

        private Task RespondAsync(HttpListenerContext context, string message, string contentType, CancellationToken token)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            return context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length, token);
        }

        private Task RespondJsonAsync<T>(HttpListenerContext context, T payload, CancellationToken token)
        {
            var json = JsonSerializer.Serialize(payload);
            return RespondAsync(context, json, "application/json", token);
        }

        public async Task StopAsync()
        {
            try
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
            }
            catch { }

            try
            {
                if (_listener.IsListening)
                {
                    _listener.Stop();
                }
            }
            catch { }

            if (_listenerTask != null)
            {
                await Task.WhenAny(_listenerTask, Task.Delay(500)).ConfigureAwait(false);
            }
        }

        private string NormalizePrefix(string endpoint)
        {
            var baseUrl = string.IsNullOrWhiteSpace(endpoint) ? "http://localhost:5000" : endpoint;
            if (!baseUrl.EndsWith("/"))
            {
                baseUrl += "/";
            }
            return baseUrl;
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener.Close(); } catch { }
            try { _synthesizer.Dispose(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _listenerTask = null;
        }

        private record TtsRequest(string Text, string? Voice, float Speed);
    }
}
