using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
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
                var hermesExe = ResolveHermesExecutable();
                if (hermesExe == null)
                {
                    Debug.WriteLine("HermesGatewayService: 'hermes' executable not found. Run Tools/setup-hermes-integration.ps1");
                    return false;
                }

                var envFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hermes", ".env");
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

        internal static string? ResolveHermesExecutable()
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir.Trim(), OperatingSystem.IsWindows() ? "hermes.exe" : "hermes");
                if (File.Exists(candidate))
                    return candidate;
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var fallbacks = new[]
            {
                Path.Combine(localAppData, "hermes", "hermes-agent", ".venv", "Scripts", "hermes.exe"),
                Path.Combine(localAppData, "Programs", "hermes", "hermes.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "hermes")
            };

            foreach (var path in fallbacks)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
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
