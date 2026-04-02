using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Memory
{
    /// <summary>
    /// Creates text embeddings via Ollama <c>/api/embed</c> (current) with fallback to <c>/api/embeddings</c>.
    /// Falls back to deterministic pseudo-embeddings when Ollama is unavailable or config is missing.
    /// </summary>
    public static class OllamaEmbeddingClient
    {
        private static readonly HttpClient Http = new(new HttpClientHandler())
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        public static Task<double[]> CreateEmbeddingAsync(string text, AppConfig? config, CancellationToken cancellationToken = default)
        {
            var dims = config?.EmbeddingVectorDimensions > 0 ? config.EmbeddingVectorDimensions : 768;
            if (config == null || string.IsNullOrWhiteSpace(config.OllamaEndpoint))
                return Task.FromResult(EmbeddingHelper.CreatePseudoEmbedding(text, dims));

            return CreateEmbeddingInternalAsync(text, config, dims, cancellationToken);
        }

        private static async Task<double[]> CreateEmbeddingInternalAsync(string text, AppConfig config, int fallbackDims, CancellationToken cancellationToken)
        {
            var baseUrl = config.OllamaEndpoint.TrimEnd('/');
            var model = string.IsNullOrWhiteSpace(config.OllamaEmbeddingModel)
                ? "nomic-embed-text"
                : config.OllamaEmbeddingModel;

            try
            {
                // Ollama 0.5+ uses POST /api/embed with { "model", "input" }
                var embedPayload = $"{{\"model\":{JsonSerializer.Serialize(model)},\"input\":{JsonSerializer.Serialize(text ?? string.Empty)}}}";
                var res = await PostJsonAsync($"{baseUrl}/api/embed", embedPayload, cancellationToken).ConfigureAwait(false);
                if (!res.Success)
                {
                    var legacy = $"{{\"model\":{JsonSerializer.Serialize(model)},\"prompt\":{JsonSerializer.Serialize(text ?? string.Empty)}}}";
                    res = await PostJsonAsync($"{baseUrl}/api/embeddings", legacy, cancellationToken).ConfigureAwait(false);
                }

                if (res.Success && res.Embedding != null && res.Embedding.Length > 0)
                    return res.Embedding;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OllamaEmbeddingClient: {ex.Message}");
            }

            return EmbeddingHelper.CreatePseudoEmbedding(text, fallbackDims);
        }

        private static async Task<(bool Success, double[]? Embedding)> PostJsonAsync(string url, string json, CancellationToken cancellationToken)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var resp = await Http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return (false, null);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            // /api/embed returns { "embeddings": [[...]] } or single embedding
            if (root.TryGetProperty("embeddings", out var embeddingsEl) && embeddingsEl.ValueKind == JsonValueKind.Array)
            {
                var first = embeddingsEl.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Array)
                    return (true, ReadDoubleArray(first));
            }

            if (root.TryGetProperty("embedding", out var single))
                return (true, ReadDoubleArray(single));

            return (false, null);
        }

        private static double[] ReadDoubleArray(JsonElement arr)
        {
            if (arr.ValueKind != JsonValueKind.Array)
                return Array.Empty<double>();
            var list = new List<double>();
            foreach (var x in arr.EnumerateArray())
            {
                if (x.ValueKind == JsonValueKind.Number && x.TryGetDouble(out var d))
                    list.Add(d);
            }
            return list.ToArray();
        }
    }
}
