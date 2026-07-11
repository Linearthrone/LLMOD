using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Core.Utils;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace HouseVictoria.Services.Hermes
{
    /// <summary>
    /// Starts and health-checks the Hermes Agent gateway (API server on port 8642).
    /// </summary>
    public class HermesGatewayService : IHermesGatewayService
    {
        private readonly AppConfig _config;
        private readonly HttpClient _httpClient;
        private readonly string _rootDirectory;

        public HermesGatewayService(AppConfig config, string? rootDirectory = null)
        {
            _config = config;
            _rootDirectory = rootDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        private string BaseUrl => NormalizeBaseUrl(_config.HermesEndpoint);

        private static string NormalizeBaseUrl(string? endpoint)
        {
            var url = (string.IsNullOrWhiteSpace(endpoint) ? "http://127.0.0.1:8642" : endpoint).Trim().TrimEnd('/');
            if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                url = url[..^3];
            return url;
        }

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _httpClient.GetAsync($"{BaseUrl}/health", cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return false;

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return body.Contains("ok", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (!await IsAvailableAsync(cancellationToken).ConfigureAwait(false))
                return false;

            if (string.IsNullOrWhiteSpace(_config.HermesApiKey))
                return true;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/v1/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.HermesApiKey);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EnsureGatewayRunningAsync(CancellationToken cancellationToken = default)
        {
            if (!ShouldAutoStart())
                return await IsAvailableAsync(cancellationToken).ConfigureAwait(false);

            if (await IsAvailableAsync(cancellationToken).ConfigureAwait(false))
                return true;

            try
            {
                var hermesExe = HermesPaths.ResolveHermesExecutable();
                if (hermesExe == null)
                {
                    Debug.WriteLine("HermesGatewayService: 'hermes' executable not found. Run Tools/setup-hermes-integration.ps1");
                    return false;
                }

                var hermesDir = HermesPaths.ResolveHermesDir();
                var envFile = Path.Combine(hermesDir, ".env");
                LoggingHelper.WriteToStartupLog($"Hermes config dir resolved: {hermesDir} (.env: {(File.Exists(envFile) ? "found" : "missing")})");
                var startInfo = new ProcessStartInfo
                {
                    FileName = hermesExe,
                    Arguments = "gateway",
                    WorkingDirectory = _rootDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                ApplyHermesEnvironment(startInfo, envFile);
                SanitizePythonEnvironment(startInfo);
                startInfo.Environment["FILE_RETRIEVAL_PATH"] = ResolveGeneratedFilesPath();

                Process.Start(startInfo);

                for (var i = 0; i < 30; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                    if (await IsAvailableAsync(cancellationToken).ConfigureAwait(false))
                        return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HermesGatewayService: failed to start gateway: {ex.Message}");
                return false;
            }
        }

        private bool ShouldAutoStart()
        {
            if (_config.HermesAutoStart)
                return true;

            return string.Equals(_config.PrimaryLLM, "hermes", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Hermes gateway runs on Python 3.11. Inherited PYTHONPATH/VIRTUAL_ENV from a project
        /// venv (e.g. Python 3.13) breaks pydantic_core native imports.
        /// </summary>
        private static void SanitizePythonEnvironment(ProcessStartInfo startInfo)
        {
            foreach (var key in new[] { "PYTHONPATH", "VIRTUAL_ENV", "VIRTUAL_ENV_PROMPT", "CONDA_PREFIX", "CONDA_DEFAULT_ENV" })
            {
                startInfo.Environment.Remove(key);
            }

            PrependPathIfExists(startInfo, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Cua", "cua-driver", "bin"));
        }

        private static void PrependPathIfExists(ProcessStartInfo startInfo, string directory)
        {
            if (!Directory.Exists(directory))
                return;

            startInfo.Environment.TryGetValue("PATH", out var path);
            if (string.IsNullOrEmpty(path))
                path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            if (!path.Split(';').Any(p => string.Equals(p.TrimEnd('\\'), directory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                startInfo.Environment["PATH"] = directory + ";" + path;
        }

        private static void ApplyHermesEnvironment(ProcessStartInfo startInfo, string envFilePath)
        {
            if (File.Exists(envFilePath))
            {
                foreach (var line in File.ReadAllLines(envFilePath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                        continue;

                    var eq = trimmed.IndexOf('=');
                    if (eq <= 0)
                        continue;

                    var key = trimmed[..eq].Trim();
                    var value = trimmed[(eq + 1)..].Trim().Trim('"');
                    startInfo.Environment[key] = value;
                }
            }
        }

        private string ResolveGeneratedFilesPath()
        {
            var mediaPath = _config.MediaPath ?? "Media";
            if (!Path.IsPathRooted(mediaPath))
                mediaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, mediaPath);
            var generated = Path.Combine(mediaPath, "GeneratedFiles");
            Directory.CreateDirectory(generated);
            return generated;
        }
    }
}
