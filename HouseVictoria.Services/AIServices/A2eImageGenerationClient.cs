using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HouseVictoria.Services.AIServices
{
    /// <summary>
    /// Cloud image generation via A2E (https://video.a2e.ai) text-to-image API.
    /// </summary>
    public sealed class A2eImageGenerationClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiToken;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public A2eImageGenerationClient(HttpClient httpClient, string apiToken, string? baseUrl = null)
        {
            _httpClient = httpClient;
            _apiToken = apiToken.Trim();
            _baseUrl = (baseUrl ?? "https://video.a2e.ai").TrimEnd('/');
        }

        public static string? ResolveApiToken(string? configToken) =>
            !string.IsNullOrWhiteSpace(configToken)
                ? configToken.Trim()
                : Environment.GetEnvironmentVariable("A2E_API_TOKEN")?.Trim();

        public static bool ShouldUseA2e(string? imageGenerationProvider, string? apiToken) =>
            !string.Equals(imageGenerationProvider?.Trim(), "comfyui", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(ResolveApiToken(apiToken));

        public async Task<Stream> GenerateImageAsync(
            string prompt,
            int width = 1024,
            int height = 768,
            string styleKey = "high_aes_general_v21_L",
            CancellationToken cancellationToken = default)
        {
            var (taskId, immediateUrl) = await StartTextToImageAsync(prompt, width, height, styleKey, cancellationToken).ConfigureAwait(false);
            var imageUrl = immediateUrl ?? await WaitForImageUrlAsync(taskId, cancellationToken).ConfigureAwait(false);
            var bytes = await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken).ConfigureAwait(false);
            return new MemoryStream(bytes);
        }

        private async Task<(string TaskId, string? ImmediateImageUrl)> StartTextToImageAsync(
            string prompt,
            int width,
            int height,
            string styleKey,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/userText2image/start");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
            request.Content = JsonContent.Create(new
            {
                name = "HouseVictoria",
                prompt,
                req_key = styleKey,
                width,
                height
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"A2E text-to-image start failed: {(int)response.StatusCode} {response.ReasonPhrase}. {Truncate(body)}");

            var parsed = JsonSerializer.Deserialize<A2eEnvelope<A2eText2ImageTask>>(body, JsonOptions);
            if (parsed?.Code != 0)
                throw new InvalidOperationException($"A2E text-to-image start returned code {parsed?.Code}. {Truncate(body)}");

            var task = parsed.Data;
            if (task == null)
                throw new InvalidOperationException($"A2E text-to-image start returned no task data. {Truncate(body)}");

            if (string.IsNullOrWhiteSpace(task.Id))
                throw new InvalidOperationException($"A2E text-to-image start did not return a task id. {Truncate(body)}");

            if (string.Equals(task.CurrentStatus, "completed", StringComparison.OrdinalIgnoreCase))
            {
                var immediate = task.ImageUrls?.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
                if (!string.IsNullOrWhiteSpace(immediate))
                    return (task.Id, immediate);
            }

            return (task.Id, null);
        }

        private async Task<string> WaitForImageUrlAsync(string taskId, CancellationToken cancellationToken)
        {
            const int maxAttempts = 120;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/v1/userText2image/{taskId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"A2E text-to-image status failed: {(int)response.StatusCode} {response.ReasonPhrase}. {Truncate(body)}");

                var parsed = JsonSerializer.Deserialize<A2eEnvelope<A2eText2ImageTask>>(body, JsonOptions);
                if (parsed?.Code != 0)
                    throw new InvalidOperationException($"A2E text-to-image status returned code {parsed?.Code}. {Truncate(body)}");

                var task = parsed.Data;
                if (task == null)
                    throw new InvalidOperationException($"A2E text-to-image status returned no data for task {taskId}.");

                var status = task.CurrentStatus ?? string.Empty;
                if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    var url = task.ImageUrls?.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
                    if (!string.IsNullOrWhiteSpace(url))
                        return url!;
                    throw new InvalidOperationException($"A2E task {taskId} completed but no image_urls were returned.");
                }

                if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(task.FailedMessage))
                {
                    var msg = string.IsNullOrWhiteSpace(task.FailedMessage) ? status : task.FailedMessage;
                    throw new InvalidOperationException($"A2E text-to-image task {taskId} failed: {msg}");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException($"A2E text-to-image task {taskId} did not complete within {maxAttempts * 2} seconds.");
        }

        private static string Truncate(string? s, int max = 400) =>
            string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");

        private sealed class A2eEnvelope<T>
        {
            [JsonPropertyName("code")]
            public int Code { get; set; }

            [JsonPropertyName("data")]
            public T? Data { get; set; }
        }

        private sealed class A2eText2ImageTask
        {
            [JsonPropertyName("_id")]
            public string? Id { get; set; }

            [JsonPropertyName("current_status")]
            public string? CurrentStatus { get; set; }

            [JsonPropertyName("image_urls")]
            public List<string>? ImageUrls { get; set; }

            [JsonPropertyName("failed_message")]
            public string? FailedMessage { get; set; }
        }
    }
}
