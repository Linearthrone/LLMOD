using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace HouseVictoria.Services.AgentDesktop
{
    /// <summary>
    /// HTTP client for the browser capture bridge (:17891). Streams active-tab PNGs
    /// from the Chrome/Edge extension into the desktop live preview.
    /// </summary>
    internal static class BrowserCaptureBridgeClient
    {
        private const string BaseUrl = "http://127.0.0.1:17891";
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        private static readonly BrowserCastSocketClient CastSocket = new();

        public static BrowserCastSocketClient Cast => CastSocket;

        public static void StartCastConsumer() => CastSocket.EnsureConnected();

        public static void StopCastConsumer() => CastSocket.Stop();

        public static bool TrySetStreamEnabled(bool enabled)
        {
            try
            {
                var body = JsonSerializer.Serialize(new { enabled });
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = Http.PostAsync($"{BaseUrl}/stream/enable", content).GetAwaiter().GetResult();
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsStreamEnabled()
        {
            try
            {
                using var response = Http.GetAsync($"{BaseUrl}/stream/status").GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return false;
                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var status = JsonSerializer.Deserialize<StreamStatusDto>(json, JsonOptions);
                return status?.StreamEnabled == true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsHealthy()
        {
            try
            {
                using var response = Http.GetAsync($"{BaseUrl}/health").GetAwaiter().GetResult();
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the latest extension-pushed browser tab frame if fresh enough.
        /// Uses /latest/meta + /latest.png to avoid deserializing huge JSON base64 blobs.
        /// </summary>
        public static (int Width, int Height, byte[] Bgra32, string SourceLabel)? TryGetLatestStreamFrame(
            int maxWidth = 960,
            double maxAgeSeconds = 5.0)
        {
            var fromSocket = CastSocket.TryGetCachedFrame(maxWidth, maxAgeSeconds);
            if (fromSocket != null)
                return fromSocket;

            try
            {
                using var metaResponse = Http.GetAsync($"{BaseUrl}/latest/meta").GetAwaiter().GetResult();
                if (!metaResponse.IsSuccessStatusCode)
                    return null;

                var metaJson = metaResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var meta = JsonSerializer.Deserialize<LatestMetaDto>(metaJson, JsonOptions);
                if (meta?.Ok != true || meta.HasImage != true)
                    return null;

                var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (meta.CapturedAt ?? 0);
                if (age > maxAgeSeconds)
                    return null;

                using var pngResponse = Http.GetAsync($"{BaseUrl}/latest.png").GetAwaiter().GetResult();
                if (!pngResponse.IsSuccessStatusCode)
                    return null;

                var png = pngResponse.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (png.Length == 0)
                    return null;

                var decoded = WindowsScreenCapture.DecodePngToBgra(png, maxWidth);
                if (decoded == null)
                    return null;

                var label = string.IsNullOrWhiteSpace(meta.Title)
                    ? meta.Url ?? "Browser tab"
                    : meta.Title;
                return (decoded.Value.Width, decoded.Value.Height, decoded.Value.Bgra32, label);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Blocks until the extension captures the active tab (on-demand fallback).
        /// </summary>
        public static (int Width, int Height, byte[] Bgra32, string SourceLabel)? TryCaptureTabNow(
            int maxWidth = 960,
            double timeoutSeconds = 6.0)
        {
            try
            {
                var body = JsonSerializer.Serialize(new
                {
                    include_screenshot = true,
                    include_page_map = false,
                    timeout_seconds = timeoutSeconds
                });
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = Http.PostAsync($"{BaseUrl}/capture", content).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var capture = JsonSerializer.Deserialize<CaptureResultDto>(json, JsonOptions);
                if (capture?.Ok != true)
                    return null;

                if (!string.IsNullOrWhiteSpace(capture.ScreenshotPath) && File.Exists(capture.ScreenshotPath))
                {
                    var png = File.ReadAllBytes(capture.ScreenshotPath);
                    var decoded = WindowsScreenCapture.DecodePngToBgra(png, maxWidth);
                    if (decoded == null)
                        return null;

                    var label = string.IsNullOrWhiteSpace(capture.Title)
                        ? capture.Url ?? "Browser tab"
                        : capture.Title;
                    return (decoded.Value.Width, decoded.Value.Height, decoded.Value.Bgra32, label);
                }

                return TryGetLatestStreamFrame(maxWidth, maxAgeSeconds: timeoutSeconds + 2);
            }
            catch
            {
                return null;
            }
        }

        private sealed class StreamStatusDto
        {
            public bool StreamEnabled { get; set; }
        }

        private sealed class LatestMetaDto
        {
            public bool Ok { get; set; }
            public string? Url { get; set; }
            public string? Title { get; set; }
            public double? CapturedAt { get; set; }
            public bool HasImage { get; set; }
        }

        private sealed class CaptureResultDto
        {
            public bool Ok { get; set; }
            public string? Url { get; set; }
            public string? Title { get; set; }
            public string? ScreenshotPath { get; set; }
        }
    }
}
