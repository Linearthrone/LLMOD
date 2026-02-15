using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using HouseVictoria.Core.Interfaces;
using System.Speech.Synthesis;

namespace HouseVictoria.Services.TTS
{
    /// <summary>
    /// Service for Text-to-Speech using external TTS endpoint with Windows TTS fallback
    /// </summary>
    public class TTSService : ITTSService
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private readonly bool _useWindowsTTSFallback;
        private readonly string? _piperDataDir;
        private readonly string? _piperDefaultVoice;
        private SpeechSynthesizer? _windowsSynthesizer;

        public TTSService(string endpoint, bool useWindowsTTSFallback = true, string? piperDataDir = null, string? piperDefaultVoice = null)
        {
            _endpoint = endpoint?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(endpoint));
            _useWindowsTTSFallback = useWindowsTTSFallback;
            _piperDataDir = string.IsNullOrWhiteSpace(piperDataDir) ? null : piperDataDir.Trim();
            _piperDefaultVoice = string.IsNullOrWhiteSpace(piperDefaultVoice) ? null : piperDefaultVoice.Trim();
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // Initialize Windows TTS synthesizer if fallback is enabled
            if (_useWindowsTTSFallback)
            {
                try
                {
                    _windowsSynthesizer = new SpeechSynthesizer();
                    System.Diagnostics.Debug.WriteLine("Windows TTS synthesizer initialized");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize Windows TTS: {ex.Message}");
                    _windowsSynthesizer = null;
                }
            }
        }

        public async Task<byte[]?> SynthesizeSpeechAsync(string text, string? voice = null, float speed = 1.0f)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                // First, try Piper TTS format (POST to root with simple JSON)
                try
                {
                    var piperRequestBody = new Dictionary<string, object?>
                    {
                        ["text"] = text
                    };
                    var voiceToUse = !string.IsNullOrWhiteSpace(voice) ? voice : _piperDefaultVoice;
                    if (!string.IsNullOrWhiteSpace(voiceToUse))
                        piperRequestBody["voice"] = voiceToUse;
                    var piperJson = JsonSerializer.Serialize(piperRequestBody);
                    var piperContent = new StringContent(piperJson, Encoding.UTF8, "application/json");
                    
                    var piperResponse = await _httpClient.PostAsync($"{_endpoint}/", piperContent);
                    if (piperResponse.IsSuccessStatusCode)
                    {
                        var audioData = await piperResponse.Content.ReadAsByteArrayAsync();
                        if (audioData.Length > 0)
                        {
                            System.Diagnostics.Debug.WriteLine("TTS: Successfully synthesized using Piper format");
                            return audioData;
                        }
                    }
                }
                catch (Exception piperEx)
                {
                    System.Diagnostics.Debug.WriteLine($"TTS: Piper format attempt failed: {piperEx.Message}");
                    // Continue to try other formats
                }

                // Try common TTS API endpoints
                // First, try /api/tts or /tts endpoint with JSON body
                var endpoints = new[]
                {
                    $"{_endpoint}/api/tts",
                    $"{_endpoint}/tts",
                    $"{_endpoint}/api/synthesize",
                    $"{_endpoint}/synthesize"
                };

                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        // Try JSON POST request
                        var requestBody = new
                        {
                            text = text,
                            voice = voice,
                            speed = speed
                        };

                        var json = JsonSerializer.Serialize(requestBody);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        
                        var response = await _httpClient.PostAsync(endpoint, content);
                        if (response.IsSuccessStatusCode)
                        {
                            var audioData = await response.Content.ReadAsByteArrayAsync();
                            if (audioData.Length > 0)
                                return audioData;
                        }
                    }
                    catch
                    {
                        // Try next endpoint
                        continue;
                    }
                }

                // Try query parameter format: /tts?text=...
                var queryEndpoints = new[]
                {
                    $"{_endpoint}/tts?text={Uri.EscapeDataString(text)}",
                    $"{_endpoint}/api/tts?text={Uri.EscapeDataString(text)}"
                };

                if (!string.IsNullOrWhiteSpace(voice))
                {
                    queryEndpoints = queryEndpoints.SelectMany(e => new[]
                    {
                        $"{e}&voice={Uri.EscapeDataString(voice)}",
                        e
                    }).ToArray();
                }

                foreach (var endpoint in queryEndpoints)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(endpoint);
                        if (response.IsSuccessStatusCode)
                        {
                            var audioData = await response.Content.ReadAsByteArrayAsync();
                            if (audioData.Length > 0)
                                return audioData;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // If all endpoints fail, try Windows TTS fallback
                if (_useWindowsTTSFallback && _windowsSynthesizer != null)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"TTS: External service unavailable, using Windows TTS fallback");
                        return await SynthesizeWithWindowsTTSAsync(text, voice, speed);
                    }
                    catch (Exception winTtsEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"TTS: Windows TTS fallback failed: {winTtsEx.Message}");
                    }
                }

                // If all methods fail, return null
                System.Diagnostics.Debug.WriteLine($"TTS: Failed to synthesize speech for text: {text.Substring(0, Math.Min(50, text.Length))}...");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS Error: {ex.Message}\n{ex.StackTrace}");
                
                // Try Windows TTS fallback on exception if enabled
                if (_useWindowsTTSFallback && _windowsSynthesizer != null)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"TTS: Exception occurred, trying Windows TTS fallback");
                        return await SynthesizeWithWindowsTTSAsync(text, voice, speed);
                    }
                    catch (Exception winTtsEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"TTS: Windows TTS fallback also failed: {winTtsEx.Message}");
                    }
                }
                
                return null;
            }
        }

        /// <summary>
        /// Synthesizes speech using Windows built-in TTS (System.Speech)
        /// </summary>
        private Task<byte[]?> SynthesizeWithWindowsTTSAsync(string text, string? voice = null, float speed = 1.0f)
        {
            if (_windowsSynthesizer == null)
                return Task.FromResult<byte[]?>(null);

            return Task.Run(() =>
            {
                try
                {
                    // Set voice if specified
                    if (!string.IsNullOrWhiteSpace(voice))
                    {
                        try
                        {
                            _windowsSynthesizer.SelectVoice(voice);
                        }
                        catch
                        {
                            // Try to find voice by name
                            var voices = _windowsSynthesizer.GetInstalledVoices();
                            var selectedVoice = voices.FirstOrDefault(v =>
                                v.VoiceInfo.Name.Contains(voice, StringComparison.OrdinalIgnoreCase) ||
                                v.VoiceInfo.Description.Contains(voice, StringComparison.OrdinalIgnoreCase));
                            
                            if (selectedVoice != null)
                            {
                                _windowsSynthesizer.SelectVoice(selectedVoice.VoiceInfo.Name);
                            }
                        }
                    }

                    // Set speech rate (System.Speech uses -10 to 10, where 0 is normal)
                    // Convert speed (0.5-2.0) to System.Speech rate (-10 to 10)
                    var rate = (int)((speed - 1.0) * 10);
                    rate = Math.Max(-10, Math.Min(10, rate)); // Clamp to valid range
                    _windowsSynthesizer.Rate = rate;

                    // Synthesize speech to memory stream
                    using (var memoryStream = new MemoryStream())
                    {
                        _windowsSynthesizer.SetOutputToWaveStream(memoryStream);
                        _windowsSynthesizer.Speak(text);
                        
                        // Reset output to default
                        _windowsSynthesizer.SetOutputToDefaultAudioDevice();
                        
                        return memoryStream.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Windows TTS synthesis error: {ex.Message}\n{ex.StackTrace}");
                    return null;
                }
            });
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                // First check external TTS endpoint
                var endpoints = new[] { "/health", "/", "/api/health" };
                
                foreach (var path in endpoints)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync($"{_endpoint}{path}");
                        if (response.IsSuccessStatusCode)
                        {
                            System.Diagnostics.Debug.WriteLine($"TTS: External service available at {_endpoint}{path}");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"TTS: Health check failed for {_endpoint}{path}: {ex.Message}");
                        continue;
                    }
                }
                
                // If external service is not available, check Windows TTS fallback
                if (_useWindowsTTSFallback && _windowsSynthesizer != null)
                {
                    try
                    {
                        var voices = _windowsSynthesizer.GetInstalledVoices();
                        if (voices.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"TTS: Windows TTS fallback available with {voices.Count} voice(s)");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"TTS: Windows TTS check failed: {ex.Message}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("TTS: No TTS service available (external or Windows fallback)");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS: IsAvailableAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets only Piper TTS voices (from server or local data dir). Does not include Windows TTS.
        /// </summary>
        private async Task<List<string>> GetPiperVoicesOnlyAsync()
        {
            var voices = new List<string>();

            try
            {
                // Try to get voices from external TTS service (Piper)
                var voiceEndpoints = new[]
                {
                    $"{_endpoint}/api/voices",
                    $"{_endpoint}/voices",
                    $"{_endpoint}/api/models",
                    $"{_endpoint}/models"
                };

                foreach (var endpoint in voiceEndpoints)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(endpoint);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            try
                            {
                                var jsonDoc = JsonDocument.Parse(content);
                                if (jsonDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var element in jsonDoc.RootElement.EnumerateArray())
                                    {
                                        string? voiceName = null;
                                        if (element.ValueKind == System.Text.Json.JsonValueKind.String)
                                            voiceName = element.GetString();
                                        else if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
                                        {
                                            if (element.TryGetProperty("name", out var nameProp))
                                                voiceName = nameProp.GetString();
                                            else if (element.TryGetProperty("id", out var idProp))
                                                voiceName = idProp.GetString();
                                            else if (element.TryGetProperty("voice", out var voiceProp))
                                                voiceName = voiceProp.GetString();
                                        }
                                        if (!string.IsNullOrWhiteSpace(voiceName) && !voices.Contains(voiceName))
                                            voices.Add(voiceName);
                                    }
                                }
                                else if (jsonDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    if (jsonDoc.RootElement.TryGetProperty("voices", out var voicesProp) && voicesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var element in voicesProp.EnumerateArray())
                                        {
                                            string? voiceName = element.GetString();
                                            if (!string.IsNullOrWhiteSpace(voiceName) && !voices.Contains(voiceName))
                                                voices.Add(voiceName);
                                        }
                                    }
                                    else if (jsonDoc.RootElement.TryGetProperty("models", out var modelsProp) && modelsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var element in modelsProp.EnumerateArray())
                                        {
                                            string? voiceName = element.GetString();
                                            if (!string.IsNullOrWhiteSpace(voiceName) && !voices.Contains(voiceName))
                                                voices.Add(voiceName);
                                        }
                                    }
                                    else
                                    {
                                        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                                        {
                                            var voiceName = prop.Name;
                                            if (!string.IsNullOrWhiteSpace(voiceName) && !voices.Contains(voiceName))
                                                voices.Add(voiceName);
                                        }
                                    }
                                }
                                if (voices.Count > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"TTS: Found {voices.Count} voice(s) from external service");
                                    return voices;
                                }
                            }
                            catch (JsonException) { continue; }
                        }
                    }
                    catch { continue; }
                }

                // Discover Piper models from local data directory
                if (voices.Count == 0 && !string.IsNullOrEmpty(_piperDataDir) && Directory.Exists(_piperDataDir))
                {
                    try
                    {
                        var seen = new HashSet<string>(voices, StringComparer.OrdinalIgnoreCase);
                        foreach (var path in Directory.EnumerateFiles(_piperDataDir, "*.onnx", SearchOption.AllDirectories))
                        {
                            var name = Path.GetFileNameWithoutExtension(path);
                            if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
                                voices.Add(name);
                        }
                        foreach (var path in Directory.EnumerateFiles(_piperDataDir, "*.onnx.json", SearchOption.AllDirectories))
                        {
                            var fileName = Path.GetFileNameWithoutExtension(path);
                            var name = fileName.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) ? fileName[..^5] : fileName;
                            if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
                                voices.Add(name);
                        }
                        if (voices.Count > 0)
                            System.Diagnostics.Debug.WriteLine($"TTS: Found {voices.Count} Piper voice(s) from data dir: {_piperDataDir}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"TTS: Failed to discover Piper voices from {_piperDataDir}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS: GetPiperVoicesOnlyAsync error: {ex.Message}");
            }

            return voices;
        }

        public async Task<List<string>> GetAvailablePiperVoicesAsync()
        {
            return await GetPiperVoicesOnlyAsync();
        }

        public async Task<List<string>> GetAvailableVoicesAsync()
        {
            var voices = await GetPiperVoicesOnlyAsync();

            // If no Piper voices, fall back to Windows TTS for general voice list
            if (voices.Count == 0 && _useWindowsTTSFallback && _windowsSynthesizer != null)
            {
                try
                {
                    var windowsVoices = _windowsSynthesizer.GetInstalledVoices();
                    foreach (var voice in windowsVoices)
                    {
                        var voiceName = voice.VoiceInfo.Name;
                        if (!string.IsNullOrWhiteSpace(voiceName) && !voices.Contains(voiceName))
                            voices.Add(voiceName);
                    }
                    System.Diagnostics.Debug.WriteLine($"TTS: Found {windowsVoices.Count} Windows TTS voice(s)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"TTS: Failed to get Windows voices: {ex.Message}");
                }
            }

            return voices;
        }
    }
}
