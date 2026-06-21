using System.Diagnostics;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Voice
{
    /// <summary>
    /// Launches and stops the external Python streaming speech-to-speech engine
    /// (On-Device-Speech-to-Speech-Conversational-AI). Persona configuration is
    /// passed through process environment variables, which the engine's
    /// pydantic settings read with priority over its .env file.
    /// </summary>
    public class VoiceCallEngineService : IVoiceCallEngineService, IDisposable
    {
        private const string EngineFolderName = "On-Device-Speech-to-Speech-Conversational-AI";

        private readonly AppConfig _config;
        private readonly object _gate = new();
        private Process? _process;

        public VoiceCallEngineService(AppConfig config)
        {
            _config = config;
        }

        public bool IsRunning
        {
            get
            {
                lock (_gate)
                {
                    return _process is { HasExited: false };
                }
            }
        }

        public string? ActiveConversationId { get; private set; }

        public async Task<bool> StartAsync(VoiceCallEngineSession session)
        {
            if (!_config.VoiceEngineEnabled)
            {
                Debug.WriteLine("VoiceEngine: disabled in config; skipping.");
                return false;
            }

            // Only one engine instance at a time.
            await StopAsync().ConfigureAwait(false);

            var engineDir = ResolveEngineDirectory();
            if (engineDir == null)
            {
                Debug.WriteLine("VoiceEngine: could not locate engine directory.");
                return false;
            }

            var python = ResolvePythonPath(engineDir);
            var script = string.IsNullOrWhiteSpace(_config.VoiceEngineScript)
                ? "speech_to_speech.py"
                : _config.VoiceEngineScript;
            var scriptPath = Path.Combine(engineDir, script);

            if (!File.Exists(python))
            {
                Debug.WriteLine($"VoiceEngine: python not found at '{python}'.");
                return false;
            }
            if (!File.Exists(scriptPath))
            {
                Debug.WriteLine($"VoiceEngine: script not found at '{scriptPath}'.");
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = $"\"{scriptPath}\"",
                    WorkingDirectory = engineDir,
                    UseShellExecute = false, // required so we can inject environment variables
                    CreateNoWindow = !_config.VoiceEngineShowConsole
                };

                // Persona / runtime overrides (engine settings read env > .env).
                psi.EnvironmentVariables["VOICE_HEADLESS"] = "1";
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["TRANSFORMERS_VERBOSITY"] = "error";
                psi.EnvironmentVariables["HF_HUB_DISABLE_TELEMETRY"] = "1";

                if (!string.IsNullOrWhiteSpace(session.Model))
                    psi.EnvironmentVariables["LLM_MODEL"] = session.Model;
                if (!string.IsNullOrWhiteSpace(session.Backend))
                    psi.EnvironmentVariables["LLM_BACKEND"] = session.Backend;
                if (!string.IsNullOrWhiteSpace(session.OllamaChatUrl))
                    psi.EnvironmentVariables["OLLAMA_URL"] = session.OllamaChatUrl;
                if (!string.IsNullOrWhiteSpace(session.ApiKey))
                    psi.EnvironmentVariables["LLM_API_KEY"] = session.ApiKey;
                if (!string.IsNullOrWhiteSpace(session.SystemPrompt))
                    psi.EnvironmentVariables["DEFAULT_SYSTEM_PROMPT"] = session.SystemPrompt;
                if (!string.IsNullOrWhiteSpace(session.Voice))
                    psi.EnvironmentVariables["VOICE_NAME"] = session.Voice;
                if (session.Speed > 0)
                    psi.EnvironmentVariables["SPEED"] = session.Speed.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);
                if (session.Temperature > 0)
                    psi.EnvironmentVariables["LLM_TEMPERATURE"] = session.Temperature.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);

                if (_config.VoiceEngineInputGain > 0)
                    psi.EnvironmentVariables["INPUT_GAIN"] = _config.VoiceEngineInputGain.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);
                if (_config.VoiceEngineSilenceThreshold > 0)
                    psi.EnvironmentVariables["SILENCE_THRESHOLD"] = _config.VoiceEngineSilenceThreshold.ToString("0.000#", System.Globalization.CultureInfo.InvariantCulture);

                if (!string.IsNullOrWhiteSpace(_config.TTSEndpoint))
                    psi.EnvironmentVariables["CHATTERBOX_TTS_URL"] = _config.TTSEndpoint.TrimEnd('/');

                var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                process.Exited += (_, _) => Debug.WriteLine("VoiceEngine: process exited.");

                if (!process.Start())
                {
                    Debug.WriteLine("VoiceEngine: Process.Start returned false.");
                    return false;
                }

                lock (_gate)
                {
                    _process = process;
                }
                ActiveConversationId = session.ConversationId;
                Debug.WriteLine($"VoiceEngine: started (pid {process.Id}) for conversation {session.ConversationId} " +
                                $"[model={session.Model}, voice={session.Voice}].");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VoiceEngine: failed to start: {ex.Message}");
                return false;
            }
        }

        public Task StopAsync()
        {
            Process? toKill;
            lock (_gate)
            {
                toKill = _process;
                _process = null;
            }
            ActiveConversationId = null;

            if (toKill == null)
                return Task.CompletedTask;

            try
            {
                if (!toKill.HasExited)
                {
                    toKill.Kill(entireProcessTree: true);
                    toKill.WaitForExit(5000);
                }
                Debug.WriteLine("VoiceEngine: stopped.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VoiceEngine: error stopping: {ex.Message}");
            }
            finally
            {
                toKill.Dispose();
            }

            return Task.CompletedTask;
        }

        private string? ResolveEngineDirectory()
        {
            // 1. Explicit config path.
            if (!string.IsNullOrWhiteSpace(_config.VoiceEngineDirectory) &&
                Directory.Exists(_config.VoiceEngineDirectory))
            {
                return Path.GetFullPath(_config.VoiceEngineDirectory);
            }

            // 2. Walk up from the app directory looking for the engine folder.
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, EngineFolderName);
                if (Directory.Exists(candidate))
                    return Path.GetFullPath(candidate);

                var parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }

            return null;
        }

        private string ResolvePythonPath(string engineDir)
        {
            if (!string.IsNullOrWhiteSpace(_config.VoiceEnginePython))
                return _config.VoiceEnginePython;

            return Path.Combine(engineDir, ".venv", "Scripts", "python.exe");
        }

        public void Dispose()
        {
            try { StopAsync().GetAwaiter().GetResult(); }
            catch { /* best effort */ }
        }
    }
}
